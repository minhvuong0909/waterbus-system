namespace WaterbusSystem.Application.Features.Captains;

public sealed class CaptainOptions
{
    public int PreparationMinutes { get; set; } = 20;
    public bool AlertsEnabled { get; set; } = true;
    public string AlertCron { get; set; } = "* * * * *";
}
