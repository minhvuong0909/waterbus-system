using System.Text.Json;
using FluentValidation;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using WaterbusSystem.Application.Features.AiBooking;
using WaterbusSystem.Application.Features.AiBooking.Interfaces;
using WaterbusSystem.Application.Features.AiBooking.Models;
using WaterbusSystem.Infrastructure.Services.AiBooking.Plugins;

namespace WaterbusSystem.Infrastructure.Services.AiBooking;

/// <summary>
/// Adapter của IAiBookingAssistantService sử dụng Microsoft Semantic Kernel và Groq.
/// Tuân thủ nghiêm ngặt BR-AI-01:
/// - Chỉ tương tác qua tools (WaterbusBookingPlugin) được ủy quyền cho ITripSearchTool và IBookingLinkTool.
/// - Không cho phép thao tác CSDL, thanh toán hay quản trị.
/// - Dữ liệu thẻ chuyến (Trip Card) lấy trực tiếp từ kết quả tool, không lấy từ câu chữ do model tự bịa.
/// - Giới hạn tác động của prompt injection bằng tool allowlist và kiểm tra tham số;
///   xử lý timeout, cancellation và lỗi provider an toàn.
/// </summary>
public class SemanticKernelAiBookingAssistantService : IAiBookingAssistantService
{
    private readonly IOptions<GroqAssistantOptions> _options;
    private readonly ITripSearchTool _tripSearchTool;
    private readonly IBookingLinkTool _bookingLinkTool;
    private readonly IValidator<AiBookingChatRequest> _validator;
    private readonly ILogger<SemanticKernelAiBookingAssistantService> _logger;
    private readonly IChatCompletionService? _testChatCompletionService;

    private const string SystemPrompt =
        "Bạn là Trợ lý ảo tư vấn đặt vé Saigon Waterbus (TP.HCM).\n" +
        "Quy tắc an toàn nghiệp vụ bắt buộc (BR-AI-01):\n" +
        "1. Bạn CHỈ được tra cứu thông tin chuyến tàu và tạo liên kết đặt vé qua các công cụ trong WaterbusBookingPlugin.\n" +
        "2. Dữ liệu từ tool là nguồn sự thật duy nhất. Bạn TUYỆT ĐỐI KHÔNG tự bịa đặt giờ chạy, giá vé, bến dừng hoặc đường dẫn đặt vé.\n" +
        "3. Nếu người dùng chưa nêu đủ thông tin (bến khởi hành, bến đến, ngày khởi hành), hãy hỏi lại người dùng để làm rõ, không được tự suy đoán hoặc chọn mặc định.\n" +
        "4. Khi tool thông báo không tìm thấy chuyến, hãy thông báo lịch sự rằng không có chuyến phù hợp, không được tự bịa ra chuyến tàu nào khác.\n" +
        "5. Bạn TUYỆT ĐỐI KHÔNG thực hiện thao tác quản trị, xóa chuyến, thanh toán hay sửa đổi CSDL. Bạn không có quyền hạn này.\n" +
        "6. Trả lời bằng tiếng Việt lịch sự, súc tích, văn phong thân thiện.";

    public SemanticKernelAiBookingAssistantService(
        IOptions<GroqAssistantOptions> options,
        ITripSearchTool tripSearchTool,
        IBookingLinkTool bookingLinkTool,
        IValidator<AiBookingChatRequest> validator,
        ILogger<SemanticKernelAiBookingAssistantService> logger,
        IChatCompletionService? testChatCompletionService = null)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _tripSearchTool = tripSearchTool ?? throw new ArgumentNullException(nameof(tripSearchTool));
        _bookingLinkTool = bookingLinkTool ?? throw new ArgumentNullException(nameof(bookingLinkTool));
        _validator = validator ?? throw new ArgumentNullException(nameof(validator));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _testChatCompletionService = testChatCompletionService;
    }

    public async Task<AiBookingChatResponse> ProcessChatAsync(
        AiBookingChatRequest request,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        // 1. Validate request đầu vào
        var validationResult = await _validator.ValidateAsync(request, cancellationToken);
        if (!validationResult.IsValid)
        {
            throw new ValidationException(validationResult.Errors);
        }

        var message = request.Message.Trim();

        // Từ khóa không phải ranh giới bảo mật: chỉ expose hai plugin read-only/booking-link.
        var config = _options.Value;

        if (!config.IsEnabled || string.IsNullOrWhiteSpace(config.ApiKey))
        {
            throw new AiBookingUnavailableException();
        }
        if (config.MaxToolCallsPerTurn is < 1 or > 20 || config.TimeoutSeconds is < 1 or > 120)
            throw new InvalidOperationException("Giới hạn tool call hoặc timeout của AI Booking Assistant không hợp lệ.");
        if (string.IsNullOrWhiteSpace(config.ModelId))
            throw new InvalidOperationException("Thiếu cấu hình model cho AI Booking Assistant.");

        // 4. Khởi tạo Plugin và Kernel
        var plugin = new WaterbusBookingPlugin(_tripSearchTool, _bookingLinkTool, config.MaxToolCallsPerTurn);

        Kernel kernel;
        IChatCompletionService chatService;

        if (_testChatCompletionService != null)
        {
            var builder = Kernel.CreateBuilder();
            builder.Plugins.AddFromObject(plugin, "WaterbusBookingPlugin");
            kernel = builder.Build();
            chatService = _testChatCompletionService;
        }
        else
        {
            var builder = Kernel.CreateBuilder();
            // Groq cung cấp Chat Completions tương thích OpenAI; vẫn giữ Semantic Kernel
            // để thực thi WaterbusBookingPlugin, chỉ đổi endpoint và credential.
            builder.AddOpenAIChatCompletion(config.ModelId, new Uri(GroqAssistantOptions.Endpoint), config.ApiKey!);

            builder.Plugins.AddFromObject(plugin, "WaterbusBookingPlugin");
            kernel = builder.Build();
            chatService = kernel.GetRequiredService<IChatCompletionService>();
        }

        // 5. Cấu hình ChatHistory và ExecutionSettings
        var chatHistory = new ChatHistory();
        chatHistory.AddSystemMessage(SystemPrompt);

        foreach (var turn in request.History)
        {
            if (turn.Role == "user") chatHistory.AddUserMessage(turn.Content);
            else chatHistory.AddAssistantMessage(turn.Content);
        }

        var userPrompt = message;
        if (!string.IsNullOrWhiteSpace(request.PreferredServiceType))
        {
            userPrompt += $" (Loại dịch vụ mong muốn: {request.PreferredServiceType})";
        }
        chatHistory.AddUserMessage(userPrompt);

        var executionSettings = new OpenAIPromptExecutionSettings
        {
            // GPT-OSS trên Groq hỗ trợ tool calling nhưng không hỗ trợ parallel tool calls.
            FunctionChoiceBehavior = FunctionChoiceBehavior.Auto(
                options: new FunctionChoiceBehaviorOptions { AllowParallelCalls = false })
        };

        var timeout = TimeSpan.FromSeconds(config.TimeoutSeconds);
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(timeout);

        // 6. Thực thi hội thoại với model
        try
        {
            var completions = await chatService.GetChatMessageContentsAsync(
                chatHistory,
                executionSettings,
                kernel,
                cts.Token);

            // Không trả câu chữ model như một nguồn dữ liệu lịch/giá/link đã xác thực.
            var answerText = plugin.InterceptedCards.Count > 0
                ? $"Tìm thấy {plugin.InterceptedCards.Count} chuyến phù hợp. Vui lòng xem thông tin chuyến bên dưới."
                : plugin.SearchNeedsClarification
                    ? "Vui lòng cho biết bến khởi hành, bến đến và ngày đi để tôi tra cứu chính xác."
                    : plugin.SearchFailed
                        ? "Hệ thống tìm chuyến đang gặp lỗi. Vui lòng thử lại sau."
                        : plugin.SearchAttempted
                            ? "Không tìm thấy chuyến phù hợp với yêu cầu của quý khách."
                            : SafeClarification(completions.LastOrDefault()?.Content)
                              ?? "Tôi có thể giúp tra cứu chuyến tàu. Quý khách muốn đi từ bến nào đến bến nào, vào ngày nào?";

            return new AiBookingChatResponse(
                TextAnswer: answerText,
                Cards: plugin.InterceptedCards,
                ExecutedTools: plugin.ExecutedTools,
                IsFallback: plugin.SearchFailed);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested && cts.IsCancellationRequested)
        {
            _logger.LogWarning("Tác vụ AI Assistant bị quá thời gian chờ ({TimeoutSeconds}s).", timeout.TotalSeconds);
            return new AiBookingChatResponse(
                TextAnswer: "Xin lỗi quý khách, hệ thống đang xử lý chậm quá thời gian quy định. Quý khách vui lòng thử lại sau giây lát.",
                Cards: Array.Empty<AiTripCardDto>(),
                ExecutedTools: plugin.ExecutedTools,
                IsFallback: true);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Tác vụ AI Assistant bị hủy bởi người gọi.");
            throw;
        }
        catch (Exception ex)
        {
            // SDK exception có thể chứa prompt/credential; chỉ ghi loại lỗi và HTTP status an toàn.
            _logger.LogError("Dịch vụ Groq qua Semantic Kernel lỗi ({ExceptionType}, HTTP {StatusCode}, mã {ProviderCode}).",
                ex.GetType().Name, SafeHttpStatusCode(ex), SafeProviderErrorCode(ex));
            // Groq đôi khi từ chối lượt suy luận tiếp sau tool (tool_use_failed).
            // Nếu SearchTrips đã chạy, kết quả đã qua ranh giới tool vẫn dùng được;
            // không dùng câu chữ model và không biến lỗi provider thành chuyến bịa.
            if (SafeHttpStatusCode(ex) == 400 && SafeProviderErrorCode(ex) == "tool_use_failed"
                && plugin.SearchAttempted)
            {
                return new AiBookingChatResponse(
                    plugin.SearchNeedsClarification
                        ? "Vui lòng cho biết bến khởi hành, bến đến và ngày đi để tôi tra cứu chính xác."
                        : plugin.SearchFailed
                        ? "Hệ thống tìm chuyến đang gặp lỗi. Vui lòng thử lại sau."
                        : plugin.InterceptedCards.Count > 0
                        ? $"Tìm thấy {plugin.InterceptedCards.Count} chuyến phù hợp. Vui lòng xem thông tin chuyến bên dưới."
                        : "Không tìm thấy chuyến phù hợp với yêu cầu của quý khách.",
                    plugin.InterceptedCards, plugin.ExecutedTools, plugin.SearchFailed);
            }
            return new AiBookingChatResponse(
                TextAnswer: "Xin lỗi quý khách, hiện tại dịch vụ trợ lý ảo đang gặp sự cố kết nối. Quý khách vui lòng thử lại sau.",
                Cards: Array.Empty<AiTripCardDto>(),
                ExecutedTools: plugin.ExecutedTools,
                IsFallback: true);
        }
    }

    private static string? SafeClarification(string? content)
    {
        var answer = content?.Trim();
        // Chỉ dùng lời AI để hỏi thêm, không dùng nó làm nguồn lịch/giá/link hay khẳng định nghiệp vụ.
        if (string.IsNullOrWhiteSpace(answer) || answer.Length > 400 || !answer.Contains('?')
            || (!answer.Contains("bến", StringComparison.OrdinalIgnoreCase)
                && !answer.Contains("ngày", StringComparison.OrdinalIgnoreCase))
            || answer.Any(char.IsDigit) || answer.Contains("http", StringComparison.OrdinalIgnoreCase)
            || answer.Contains("giá", StringComparison.OrdinalIgnoreCase)
            || answer.Contains("thanh toán", StringComparison.OrdinalIgnoreCase)
            || answer.Contains("đã", StringComparison.OrdinalIgnoreCase))
            return null;
        return answer;
    }

    private static int? SafeHttpStatusCode(Exception exception)
    {
        for (var current = exception; current != null; current = current.InnerException)
        {
            if (current is HttpRequestException httpException && httpException.StatusCode.HasValue)
                return (int)httpException.StatusCode.Value;

            // Semantic Kernel/OpenAI-compatible SDK có thể bọc HTTP error trong kiểu exception riêng.
            var value = current.GetType().GetProperty("StatusCode")?.GetValue(current);
            if (value is System.Net.HttpStatusCode status)
                return (int)status;
            if (value is int code && code is >= 100 and <= 599)
                return code;
        }
        return null;
    }

    private static string SafeProviderErrorCode(Exception exception)
    {
        for (var current = exception; current is not null; current = current.InnerException)
        {
            if (current is not HttpOperationException { ResponseContent: { Length: > 0 } content })
                continue;

            if (content.Length > 16384)
                return "unavailable";

            try
            {
                using var document = JsonDocument.Parse(content);
                if (document.RootElement.ValueKind != JsonValueKind.Object
                    || !document.RootElement.TryGetProperty("error", out var error)
                    || error.ValueKind != JsonValueKind.Object)
                    return "unavailable";

                foreach (var name in new[] { "code", "type" })
                {
                    if (!error.TryGetProperty(name, out var value) || value.ValueKind != JsonValueKind.String)
                        continue;

                    var code = value.GetString();
                    // Chỉ ghi một mã dạng identifier; tuyệt đối không log message/failed_generation/prompt.
                    if (!string.IsNullOrEmpty(code) && code.Length <= 60
                        && code.All(c => c is >= 'a' and <= 'z' or >= '0' and <= '9' or '_'))
                        return code;
                }
            }
            catch (JsonException)
            {
                return "unavailable";
            }
        }

        return "unavailable";
    }
}
