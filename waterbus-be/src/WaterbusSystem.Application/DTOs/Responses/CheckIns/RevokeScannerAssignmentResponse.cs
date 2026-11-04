namespace WaterbusSystem.Application.DTOs.Responses.CheckIns;

public record RevokeScannerAssignmentResponse(Guid AssignmentId, DateTimeOffset ActiveUntil);
