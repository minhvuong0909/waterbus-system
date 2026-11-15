using Microsoft.Extensions.Options;
using Moq;
using WaterbusSystem.Application.Common.Interfaces;
using WaterbusSystem.Application.Features.Trips;
using WaterbusSystem.Infrastructure.Services;

namespace WaterbusSystem.UnitTests.TestHelpers;

/// <summary>Phụ thuộc dùng cho TripService trong test: quy tắc thuyền trưởng nới lỏng (chỉ theo vòng đời trạng thái), không có GPS.</summary>
public static class TestCaptainRules
{
    public static IBoatPositionCache Positions => Mock.Of<IBoatPositionCache>();
    public static IGeoDistanceCalculator Distance => new NtsGeoDistanceCalculator();
    public static IOptions<CaptainOperationOptions> Relaxed => Options.Create(new CaptainOperationOptions { Enforce = false });
}
