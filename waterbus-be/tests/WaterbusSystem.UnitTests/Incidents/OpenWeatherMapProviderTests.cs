using System.Net;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using WaterbusSystem.Application.Features.Incidents;
using WaterbusSystem.Infrastructure.Services.Weather;
using Xunit;

namespace WaterbusSystem.UnitTests.Incidents;

public class OpenWeatherMapProviderTests
{
    private const string SampleJson = """
        {"weather":[{"id":211,"main":"Thunderstorm","description":"dông"}],
         "wind":{"speed":12.5,"deg":200,"gust":19.3},"dt":1791763200}
        """;

    private sealed class StubHandler(Queue<Func<HttpResponseMessage>> responses) : HttpMessageHandler
    {
        public int Calls { get; private set; }
        public Uri? LastUri { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Calls++;
            LastUri = request.RequestUri;
            return Task.FromResult(responses.Dequeue()());
        }
    }

    private static (OpenWeatherMapProvider Provider, StubHandler Handler) Create(int maxRetries, params Func<HttpResponseMessage>[] responses)
    {
        var handler = new StubHandler(new Queue<Func<HttpResponseMessage>>(responses));
        var client = new HttpClient(handler) { BaseAddress = new Uri("https://api.openweathermap.org/data/2.5/") };
        var options = Options.Create(new WeatherOptions { ApiKey = "test-key", MaxRetries = maxRetries });
        return (new OpenWeatherMapProvider(client, options, NullLogger<OpenWeatherMapProvider>.Instance), handler);
    }

    private static HttpResponseMessage Ok() => new(HttpStatusCode.OK) { Content = new StringContent(SampleJson) };
    private static HttpResponseMessage Status(HttpStatusCode code) => new(code);

    [Fact]
    public async Task GetCurrent_ShouldMapWindGustConditionAndTime()
    {
        var (provider, handler) = Create(2, Ok);

        var observation = await provider.GetCurrentAsync(10.7769, 106.7009);

        observation.WindSpeedMs.Should().Be(12.5);
        observation.WindGustMs.Should().Be(19.3);
        observation.ConditionCode.Should().Be(211);
        observation.Description.Should().Be("dông");
        observation.ObservedAt.Should().Be(DateTimeOffset.FromUnixTimeSeconds(1791763200));
        handler.LastUri!.Query.Should().Contain("lat=10.7769").And.Contain("lon=106.7009").And.Contain("units=metric");
    }

    [Fact]
    public async Task GetCurrent_TransientErrorThenSuccess_ShouldRetry()
    {
        var (provider, handler) = Create(2, () => Status(HttpStatusCode.ServiceUnavailable), Ok);

        var observation = await provider.GetCurrentAsync(10, 106);

        observation.WindSpeedMs.Should().Be(12.5);
        handler.Calls.Should().Be(2);
    }

    [Fact]
    public async Task GetCurrent_NetworkErrorThenSuccess_ShouldRetry()
    {
        var (provider, handler) = Create(2, () => throw new HttpRequestException("connection reset"), Ok);

        var observation = await provider.GetCurrentAsync(10, 106);

        observation.ConditionCode.Should().Be(211);
        handler.Calls.Should().Be(2);
    }

    [Fact]
    public async Task GetCurrent_PersistentServerError_ShouldGiveUpAfterMaxRetries()
    {
        var (provider, handler) = Create(2,
            () => Status(HttpStatusCode.InternalServerError),
            () => Status(HttpStatusCode.InternalServerError),
            () => Status(HttpStatusCode.InternalServerError));

        var act = () => provider.GetCurrentAsync(10, 106);

        await act.Should().ThrowAsync<HttpRequestException>();
        handler.Calls.Should().Be(3); // 1 lần đầu + 2 lần thử lại
    }

    [Fact]
    public async Task GetCurrent_Unauthorized_ShouldNotRetry()
    {
        var (provider, handler) = Create(2, () => Status(HttpStatusCode.Unauthorized));

        var act = () => provider.GetCurrentAsync(10, 106);

        await act.Should().ThrowAsync<HttpRequestException>();
        handler.Calls.Should().Be(1);
    }

    [Fact]
    public async Task GetCurrent_MissingApiKey_ShouldThrowWithoutCalling()
    {
        var handler = new StubHandler(new Queue<Func<HttpResponseMessage>>());
        var provider = new OpenWeatherMapProvider(
            new HttpClient(handler) { BaseAddress = new Uri("https://example.test/") },
            Options.Create(new WeatherOptions { ApiKey = "" }), NullLogger<OpenWeatherMapProvider>.Instance);

        var act = () => provider.GetCurrentAsync(10, 106);

        await act.Should().ThrowAsync<InvalidOperationException>();
        handler.Calls.Should().Be(0);
    }
}
