namespace ArcadeHub.Models;

public sealed class UserProfile
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = "";
    public string Avatar { get; set; } = "🎮";

    public bool IsAdmin { get; set; }
    public bool HasPin { get; set; }
}
