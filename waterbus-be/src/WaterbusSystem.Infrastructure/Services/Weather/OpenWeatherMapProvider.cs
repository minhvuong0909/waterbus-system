using System.Globalization;
using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using WaterbusSystem.Application.Common.Interfaces;
using WaterbusSystem.Application.Features.Incidents;

namespace WaterbusSystem.Infrastructure.Services.Weather;

/// <summary>
/// Gọi OpenWeatherMap Current Weather API (https://openweathermap.org/current).
/// Timeout lấy từ cấu hình (đặt trên HttpClient), retry có giới hạn cho lỗi tạm thời (mạng, 429, 5xx, timeout).
/// </summary>
public class OpenWeatherMapProvider : IWeatherProvider
{
    private readonly HttpClient _httpClient;
    private readonly WeatherOptions _options;
    private readonly ILogger<OpenWeatherMapProvider> _logger;

    public OpenWeatherMapProvider(HttpClient httpClient, IOptions<WeatherOptions> options, ILogger<OpenWeatherMapProvider> logger)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<WeatherObservation> GetCurrentAsync(double latitude, double longitude, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_options.ApiKey))
        {
            throw new InvalidOperationException("Thiếu cấu hình Weather:ApiKey cho OpenWeatherMap.");
        }

        var url = string.Create(CultureInfo.InvariantCulture,
            $"weather?lat={latitude}&lon={longitude}&units=metric&lang=vi&appid={Uri.EscapeDataString(_options.ApiKey)}");

        for (var attempt = 0; ; attempt++)
        {
            try
            {
                using var response = await _httpClient.GetAsync(url, cancellationToken);

                if (IsTransient(response.StatusCode) && attempt < _options.MaxRetries)
                {
                    _logger.LogWarning("OpenWeatherMap trả {Status}, thử lại lần {Attempt}.", (int)response.StatusCode, attempt + 1);
                    await DelayBeforeRetryAsync(attempt, cancellationToken);
                    continue;
                }

                response.EnsureSuccessStatusCode();
                await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
                var payload = await JsonSerializer.DeserializeAsync<OwmResponse>(stream, cancellationToken: cancellationToken)
                              ?? throw new InvalidOperationException("Phản hồi OpenWeatherMap rỗng.");
                return Map(payload);
            }
            catch (Exception ex) when (attempt < _options.MaxRetries
                                       && !cancellationToken.IsCancellationRequested
                                       && IsRetryable(ex))
            {
                _logger.LogWarning(ex, "Gọi OpenWeatherMap thất bại, thử lại lần {Attempt}.", attempt + 1);
                await DelayBeforeRetryAsync(attempt, cancellationToken);
            }
        }
    }

    /// <summary>
    /// Thử lại khi lỗi mạng (không có status), mã lỗi tạm thời, hoặc timeout của HttpClient
    /// (TaskCanceledException khi token chưa huỷ). Lỗi 4xx như sai API key thì không thử lại.
    /// </summary>
    private static bool IsRetryable(Exception ex) => ex switch
    {
        HttpRequestException { StatusCode: null } => true,
        HttpRequestException { StatusCode: { } status } => IsTransient(status),
        TaskCanceledException => true,
        _ => false
    };

    private static bool IsTransient(HttpStatusCode status) =>
        status == HttpStatusCode.TooManyRequests || (int)status >= 500;

    private static Task DelayBeforeRetryAsync(int attempt, CancellationToken cancellationToken) =>
        Task.Delay(TimeSpan.FromMilliseconds(200 * Math.Pow(2, attempt)), cancellationToken);

    internal static WeatherObservation Map(OwmResponse payload)
    {
        var condition = payload.Weather?.FirstOrDefault();
        return new WeatherObservation(
            payload.Wind?.Speed ?? 0,
            payload.Wind?.Gust,
            condition?.Id ?? 0,
            condition?.Description ?? string.Empty,
            payload.Dt > 0 ? DateTimeOffset.FromUnixTimeSeconds(payload.Dt) : DateTimeOffset.UtcNow);
    }

    internal sealed class OwmResponse
    {
        [JsonPropertyName("weather")] public List<OwmCondition>? Weather { get; set; }
        [JsonPropertyName("wind")] public OwmWind? Wind { get; set; }
        [JsonPropertyName("dt")] public long Dt { get; set; }
    }

    internal sealed class OwmCondition
    {
        [JsonPropertyName("id")] public int Id { get; set; }
        [JsonPropertyName("description")] public string? Description { get; set; }
    }

    internal sealed class OwmWind
    {
        [JsonPropertyName("speed")] public double Speed { get; set; }
        [JsonPropertyName("gust")] public double? Gust { get; set; }
    }
}
