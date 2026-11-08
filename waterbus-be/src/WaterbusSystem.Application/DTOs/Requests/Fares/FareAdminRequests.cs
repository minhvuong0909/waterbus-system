namespace WaterbusSystem.Application.DTOs.Requests.Fares;

/// <summary>Giá phẳng theo TripType x SeatClass (BR-FARE-01); chỉ hỗ trợ VND.</summary>
public record CreateFareRuleRequest(
    string TripType, Guid SeatClassId, decimal Price, string Currency, DateTimeOffset EffectiveFrom, DateTimeOffset? EffectiveTo);

/// <summary>Bảng giá đã có hiệu lực chỉ đổi được EffectiveTo; muốn đổi giá hãy tạo bảng giá mới.</summary>
public record UpdateFareRuleRequest(decimal Price, DateTimeOffset EffectiveFrom, DateTimeOffset? EffectiveTo);

public record GetFareRulesRequest(int PageNumber = 1, int PageSize = 10, string? TripType = null, Guid? SeatClassId = null, bool? IsActive = null);
