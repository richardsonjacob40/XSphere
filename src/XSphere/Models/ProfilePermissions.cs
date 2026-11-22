namespace XSphere.Models;

public sealed class ProfilePermissions
{
    public string ProfileId { get; set; } = "";

    public bool CanLaunchGames { get; set; } = true;
    public bool CanManageEmulators { get; set; } = true;
    public bool CanModifySettings { get; set; } = true;
    public bool CanManageProfiles { get; set; } = false;
}
