using Microsoft.EntityFrameworkCore;
using WaterbusSystem.Domain.Enums;
using WaterbusSystem.Infrastructure.Persistence;

namespace WaterbusSystem.Infrastructure.Repositories;

/// <summary>Batch nguyên tử, khóa riêng giữa các worker; không lấy khóa thương mại hoặc đọc Booking/Payment.</summary>
internal static class CaptainAttendanceAlertWriter
{
    internal static async Task<int> InsertAsync(ApplicationDbContext db, DateTimeOffset at, int preparationMinutes, int batchSize, CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var count = await db.Database.ExecuteSqlInterpolatedAsync($"""
            SET NOCOUNT OFF;
            DECLARE @lockResult int;
            EXEC @lockResult=sys.sp_getapplock @Resource=N'waterbus:captain-attendance',
                @LockMode='Exclusive', @LockOwner='Transaction', @LockTimeout=0;
            IF @lockResult < 0 RETURN;
            INSERT INTO [CaptainAlert]
                ([Id],[DeduplicationKey],[TripId],[BoatId],[CaptainId],[Type],[Message],[OccurredAt],[CreatedAt],[IsDeleted])
            SELECT TOP ({batchSize}) NEWID(), k.[Key], t.[Id], b.[Id], b.[CaptainUserId], N'CaptainMissing',
                N'Captain attendance is overdue. Admin must review; Trip.Status is unchanged.', {at}, {at}, 0
            FROM [Trips] t WITH (READPAST, READCOMMITTEDLOCK)
            JOIN [Boats] b WITH (READPAST, READCOMMITTEDLOCK) ON b.[Id]=t.[BoatId] AND b.[IsDeleted]=0
            CROSS APPLY (SELECT TOP (1) c.[CheckInOpenAt] FROM [TripStopCalls] c
                WHERE c.[TripId]=t.[Id] AND c.[IsDeleted]=0 AND c.[Status]<>N'Skipped'
                ORDER BY c.[VisitOrder]) firstCall
            CROSS APPLY (SELECT CONCAT(N'missing:', LOWER(CONVERT(char(36),t.[Id])), N':',
                LOWER(CONVERT(char(36),b.[CaptainAssignmentVersion]))) AS [Key]) k
            WHERE t.[IsDeleted]=0 AND t.[ArrivalTime]>={at} AND t.[Status] IN ({(int)TripStatus.Scheduled},{(int)TripStatus.Boarding})
              AND firstCall.[CheckInOpenAt] < {at.AddMinutes(preparationMinutes)}
              AND NOT EXISTS (SELECT 1 FROM [CaptainAttendance] a WHERE a.[TripId]=t.[Id] AND a.[BoatId]=b.[Id]
                  AND a.[AssignmentVersion]=b.[CaptainAssignmentVersion] AND a.[CaptainId]=b.[CaptainUserId] AND a.[IsDeleted]=0)
              AND NOT EXISTS (SELECT 1 FROM [CaptainAlert] a WHERE a.[TripId]=t.[Id] AND a.[Type]=N'CaptainMissing'
                  AND (a.[DeduplicationKey]=k.[Key] OR a.[DeduplicationKey] LIKE k.[Key]+N':%'))
            ORDER BY firstCall.[CheckInOpenAt], t.[Id];
            """, ct);
        await transaction.CommitAsync(ct);
        return Math.Max(0, count);
    }
}
