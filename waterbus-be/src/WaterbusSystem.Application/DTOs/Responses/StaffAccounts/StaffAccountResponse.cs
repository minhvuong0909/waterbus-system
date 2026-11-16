namespace WaterbusSystem.Application.DTOs.Responses.StaffAccounts;

public record StaffAccountResponse(Guid Id, string FullName, string Email, string? Phone, string? StaffCode,
    bool IsActive, bool InvitationPending, bool EmailConfirmed);
