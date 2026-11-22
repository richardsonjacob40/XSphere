namespace XSphere.Models;

public sealed class Game
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Title { get; set; } = "";
    public string System { get; set; } = "Unknown";
    public string FilePath { get; set; } = "";
    public bool IsFavorite { get; set; }
}
