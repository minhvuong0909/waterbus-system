namespace WaterbusSystem.Application.DTOs.Responses.CheckIns;

public record NoShowFinalizationResultResponse(int StopCallsEligible, int StopCallsDeferred, int TicketsFinalized);
