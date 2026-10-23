using MediatR;
using Microsoft.EntityFrameworkCore;
using WaterbusSystem.Application.Common.Interfaces;

namespace WaterbusSystem.Application.Features.Fares.Queries.GetFares;

/// <summary>
/// Một mức giá đang hiệu lực: BR-FARE-01 giá chỉ phân biệt theo TripType và SeatClass.
/// </summary>
public record FareDto(string TripType, Guid SeatClassId, string SeatClassCode, string SeatClassName, decimal Price, string Currency);

/// <summary>
/// Bảng giá công khai đang hiệu lực (chỉ đọc), để Web hiển thị "giá từ" đúng theo CSDL
/// thay vì số viết cứng. Sắp theo loại chuyến rồi giá tăng dần.
/// </summary>
public record GetFaresQuery : IRequest<List<FareDto>>;

public class GetFaresQueryHandler : IRequestHandler<GetFaresQuery, List<FareDto>>
{
    private readonly IApplicationDbContext _context;
    private readonly IDateTimeService _dateTime;

    public GetFaresQueryHandler(IApplicationDbContext context, IDateTimeService dateTime)
    {
        _context = context;
        _dateTime = dateTime;
    }

    public async Task<List<FareDto>> Handle(GetFaresQuery request, CancellationToken cancellationToken)
    {
        var now = _dateTime.Now;
        var fares = await _context.FareRules
            .AsNoTracking()
            .Where(f => !f.IsDeleted && f.IsActive
                        && f.EffectiveFrom <= now && (f.EffectiveTo == null || f.EffectiveTo > now)
                        && f.SeatClass != null && f.SeatClass.IsActive && !f.SeatClass.IsDeleted)
            .Select(f => new FareDto(f.TripType, f.SeatClassId, f.SeatClass!.Code, f.SeatClass.Name, f.Price, f.Currency))
            .ToListAsync(cancellationToken);

        // Sắp xếp phía client: InMemory/SQL đều cho cùng kết quả, giá decimal sắp đúng.
        return fares.OrderBy(f => f.TripType).ThenBy(f => f.Price).ToList();
    }
}
