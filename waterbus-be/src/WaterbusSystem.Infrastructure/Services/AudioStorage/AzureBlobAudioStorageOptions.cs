namespace WaterbusSystem.Infrastructure.Services.AudioStorage;

/// <summary>Cấu hình kho MP3 private; ConnectionString chỉ đặt ngoài Git.</summary>
public sealed class AzureBlobAudioStorageOptions
{
    public const string SectionName = "AzureBlobAudioStorage";

    public string? ConnectionString { get; set; }
    public string? ContainerName { get; set; }
}
