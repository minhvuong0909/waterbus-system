using Microsoft.EntityFrameworkCore;
using WaterbusSystem.Domain.Entities;
using WaterbusSystem.Infrastructure.Identity;

namespace WaterbusSystem.UnitTests.TestHelpers;

/// <summary>Chỉ để seed dữ liệu theo schema main trước PR Captain, không dùng trong code sản phẩm.</summary>
internal static class CaptainLegacyModel
{
    internal static void Configure(ModelBuilder builder)
    {
        builder.Ignore<CaptainAlert>(); builder.Ignore<CaptainAttendance>(); builder.Ignore<CaptainAuditEvent>();
        builder.Entity<Boat>().Ignore(b => b.CaptainAssignmentVersion);
        var user = builder.Entity<ApplicationUser>();
        user.Ignore(u => u.CaptainStatus); user.Ignore(u => u.CaptainLicenseNumber); user.Ignore(u => u.CaptainLicenseExpiresOn);
        user.Ignore(u => u.CaptainSessionVersion); user.Ignore(u => u.PersonnelInvitationPending);
    }
}
