namespace WaterbusSystem.Application.Features.Captains;

public sealed class CaptainOptions
{
    public int AlertBatchSize { get; set; } = 100;
    public int PreparationMinutes { get; set; } = 20;
    public bool AlertsEnabled { get; set; } = true;
    public string AlertCron { get; set; } = "* * * * *";
}
