using Microsoft.Extensions.Options;
using WaterbusSystem.Application.Common.Interfaces;
using WaterbusSystem.Application.Features.Incidents;

namespace WaterbusSystem.Infrastructure.Services.Weather;

/// <summary>
/// Thời tiết giả lập từ cấu hình Weather:Fake:* để demo/test mà không cần API key hay chờ trời dông thật.
/// Dùng IOptionsMonitor nên đổi cấu hình là có hiệu lực ở lượt quét sau.
/// </summary>
public class FakeWeatherProvider : IWeatherProvider
{
    private readonly IOptionsMonitor<WeatherOptions> _options;
    private readonly IDateTimeService _dateTime;

    public FakeWeatherProvider(IOptionsMonitor<WeatherOptions> options, IDateTimeService dateTime)
    {
        _options = options;
        _dateTime = dateTime;
    }

    public Task<WeatherObservation> GetCurrentAsync(double latitude, double longitude, CancellationToken cancellationToken = default)
    {
        var fake = _options.CurrentValue.Fake;
        return Task.FromResult(new WeatherObservation(
            fake.WindSpeedMs, fake.WindGustMs, fake.ConditionCode, fake.Description, _dateTime.Now));
    }
}
