namespace WaterbusSystem.Application.DTOs.Requests.OnboardAudio;

/// <summary>State: Started | Completed | Failed.</summary>
public record ReportPlaybackStateRequest(string State, string? Message = null);
