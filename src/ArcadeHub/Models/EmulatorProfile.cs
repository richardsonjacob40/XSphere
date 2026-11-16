namespace ArcadeHub.Models;

public sealed class EmulatorProfile
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = "";
    public string System { get; set; } = "";
    public string ExecutablePath { get; set; } = "";
    public string ArgumentsTemplate { get; set; } = "\"{rom}\"";
}
