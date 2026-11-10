using Microsoft.EntityFrameworkCore;
using WaterbusSystem.Application.Interfaces.Repositories;
using WaterbusSystem.Domain.Entities;
using WaterbusSystem.Domain.Enums;
using WaterbusSystem.Domain.Exceptions;
using WaterbusSystem.Infrastructure.Persistence;

namespace WaterbusSystem.Infrastructure.Repositories;

public sealed class RefundCandidateRepository(ApplicationDbContext context) : IRefundCandidateRepository
{
    public async Task<Trip> GetTripAsync(Guid tripId, CancellationToken ct) =>
        await context.Trips.AsNoTracking().Include(t => t.Route).FirstOrDefaultAsync(t => t.Id == tripId, ct)
        ?? throw new NotFoundException(nameof(Trip), tripId);

    public async Task<IReadOnlyList<Booking>> ListBookingsWithTicketsAsync(Guid tripId, CancellationToken ct) =>
        await context.Bookings.AsNoTracking()
            .Include(b => b.Order).Include(b => b.DisembarkingCall).Include(b => b.Tickets)
            .Where(b => b.TripId == tripId && b.Tickets.Any())
            .OrderBy(b => b.BookingCode).ThenBy(b => b.Id)
            .ToListAsync(ct);

    public async Task<IReadOnlySet<Guid>> GetAllocatedTicketIdsAsync(Guid tripId, CancellationToken ct) =>
        (await context.RefundTicketAllocations.AsNoTracking()
            .Where(a => a.Ticket!.Booking!.TripId == tripId && a.RefundTransaction != null &&
                a.RefundTransaction.TransactionType == FinancialTransactionType.Refund &&
                (a.RefundTransaction.Status == FinancialTransactionStatus.Requested ||
                 a.RefundTransaction.Status == FinancialTransactionStatus.Processing ||
                 a.RefundTransaction.Status == FinancialTransactionStatus.Succeeded))
            .Select(a => a.TicketId).ToListAsync(ct)).ToHashSet();
}
