using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using WaterbusSystem.Application.Features.AiBooking.Models;
using WaterbusSystem.Application.Features.AiBooking.Validators;
using WaterbusSystem.Infrastructure.Services.AiBooking;
using WaterbusSystem.UnitTests.AiBooking.Fakes;
using WaterbusSystem.WebApi.Controllers;
using Xunit;
using Xunit.Abstractions;

namespace WaterbusSystem.UnitTests.AiBooking;

/// <summary>
/// Opt-in: gọi Groq thật qua Semantic Kernel nhưng chỉ dùng trip fixture trong test.
/// Không chứng minh Trip Search/Booking link/Web production đã tích hợp.
/// </summary>
public sealed class GroqBookingLiveSmokeTests
{
    private readonly ITestOutputHelper _output;

    public GroqBookingLiveSmokeTests(ITestOutputHelper output) => _output = output;

    [Fact]
    public async Task GroqCallsSearchPluginAndReturnsStructuredCard_WhenExplicitlyEnabled()
    {
        if (Environment.GetEnvironmentVariable("WATERBUS_GROQ_BOOKING_SMOKE") != "1")
            return;

        var configuration = new ConfigurationBuilder()
            .AddUserSecrets(typeof(BaseApiController).Assembly, optional: true)
            .AddEnvironmentVariables()
            .Build();
        var apiKey = configuration["AiBookingAssistant:Groq:ApiKey"];
        if (string.IsNullOrWhiteSpace(apiKey))
            throw new InvalidOperationException("Thiếu AiBookingAssistant:Groq:ApiKey trong WebApi User Secrets hoặc biến môi trường.");

        var options = new GroqAssistantOptions
        {
            ApiKey = apiKey,
            ModelId = configuration["AiBookingAssistant:Groq:ModelId"] ?? "openai/gpt-oss-20b",
            IsEnabled = true,
            TimeoutSeconds = 60
        };
        var diagnostics = new SafeDiagnosticLogger();
        var assistant = new SemanticKernelAiBookingAssistantService(
            Options.Create(options),
            new FakeTripSearchTool(),
            new FakeBookingLinkTool(),
            new AiBookingChatRequestValidator(),
            diagnostics);
        var localDate = DateOnly.FromDateTime(DateTimeOffset.UtcNow.ToOffset(TimeSpan.FromHours(7)).DateTime.AddDays(1));

        var response = await assistant.ProcessChatAsync(new AiBookingChatRequest
        {
            Message = $"Tìm chuyến từ Bến Bạch Đằng đến Bến Linh Đông ngày {localDate:yyyy-MM-dd}."
        });

        response.IsFallback.Should().BeFalse(
            "Groq/tool diagnostic: exception={0}; httpStatus={1}; tools={2}; cards={3}",
            diagnostics.ExceptionTypes,
            diagnostics.HttpStatuses,
            string.Join(",", response.ExecutedTools.Select(t => $"{t.ToolName}:{t.Success}")),
            response.Cards.Count);
        response.ExecutedTools.Should().Contain(x => x.ToolName == "SearchTrips" && x.Success);
        response.Cards.Should().ContainSingle();
        response.Cards[0].BasePrice.Should().BeNull("fixture chưa có giá FareRule đã xác thực");
        response.Cards[0].BookingUrl.Should().BeNull("fixture chưa có booking link production");

        // Chỉ in dữ liệu fixture đã được kiểm tra, không in key/prompt hay payload provider.
        var card = response.Cards[0];
        _output.WriteLine("=== AI Booking Assistant — dữ liệu chuyến MẪU ===");
        _output.WriteLine("Trả lời: {0}", response.TextAnswer);
        _output.WriteLine("Plugin: {0}", string.Join(", ", response.ExecutedTools.Select(t => t.ToolName)));
        _output.WriteLine("Trip Card: {0}", card.RouteName);
        _output.WriteLine("Bến: {0} → {1}", card.DepartureStationName, card.ArrivalStationName);
        _output.WriteLine("Khởi hành: {0:yyyy-MM-dd HH:mm} (UTC+7)", card.DepartureTime.ToOffset(TimeSpan.FromHours(7)));
        _output.WriteLine("Giá: chưa xác thực; link đặt vé: chưa tích hợp");
    }

    /// <summary>Chỉ ghi tên loại exception; tuyệt đối không in message, request hoặc key.</summary>
    private sealed class SafeDiagnosticLogger : ILogger<SemanticKernelAiBookingAssistantService>
    {
        private readonly List<string> _exceptionTypes = new();
        public string ExceptionTypes => _exceptionTypes.Count == 0
            ? "none" : string.Join(",", _exceptionTypes);
        public string HttpStatuses => _httpStatuses.Count == 0
            ? "none" : string.Join(",", _httpStatuses);
        private readonly List<string> _httpStatuses = new();

        public IDisposable BeginScope<TState>(TState state) where TState : notnull => NoopScope.Instance;
        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state,
            Exception? exception, Func<TState, Exception?, string> formatter)
        {
            if (state is IEnumerable<KeyValuePair<string, object?>> fields)
            {
                var exceptionType = fields.FirstOrDefault(x => x.Key == "ExceptionType").Value?.ToString();
                if (!string.IsNullOrWhiteSpace(exceptionType))
                    _exceptionTypes.Add(exceptionType);
                var status = fields.FirstOrDefault(x => x.Key == "StatusCode").Value?.ToString();
                if (!string.IsNullOrWhiteSpace(status))
                    _httpStatuses.Add(status);
            }
            if (exception is not null)
            {
                var chain = new List<string>();
                for (var current = exception; current is not null; current = current.InnerException)
                    chain.Add(current.GetType().Name);
                _exceptionTypes.Add(string.Join("->", chain));
            }
        }

        private sealed class NoopScope : IDisposable
        {
            public static readonly NoopScope Instance = new();
            public void Dispose() { }
        }
    }
}
