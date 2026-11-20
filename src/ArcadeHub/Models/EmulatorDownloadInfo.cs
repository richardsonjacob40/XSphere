namespace ArcadeHub.Models;

public sealed class EmulatorDownloadInfo
{
    public string Id { get; init; } = "";
    public string Name { get; init; } = "";
    public string System { get; init; } = "";

    public string OfficialWebsite { get; init; } = "";

    public string DownloadUrl { get; init; } = "";
    public string ExecutableName { get; init; } = "";

    public string FlatpakAppId { get; init; } = "";
    public string WingetPackageId { get; init; } = "";

    public bool? FlatpakPublisherVerified { get; init; }

    public string DefaultArguments { get; init; } = "\"{rom}\"";

    public string Platform { get; init; } = "";
    public string Architecture { get; init; } = "";
}
