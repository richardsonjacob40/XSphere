using System;
using System.IO;
using System.Text.Json;

namespace XSphere.Services;

public sealed class AppSettings
{
    public bool MinimizeOnLaunch { get; set; } = true;
    public bool RestoreAfterExit { get; set; } = true;
    public bool AutoRotateFeatured { get; set; } = true;
    public int FeaturedRotationSeconds { get; set; } = 8;
    public string Theme { get; set; } = "Emerald Glass";
    public string BackgroundStyle { get; set; } = "Solid Color";
    public double BackgroundIntensity { get; set; } = 35;
    public double GlassTransparency { get; set; } = 20;
    public double GlowIntensity { get; set; } = 35;
    public double HomeGlassTransparency { get; set; } = 30;
    public double HomeGlowIntensity { get; set; } = 50;




}

public sealed class AppSettingsService
{
    private readonly string _path;

    public AppSettingsService()
    {
        var folder = Path.Combine(
            XSphereDataMigration.XSphereDirectory);

        _path = Path.Combine(folder, "settings.json");
    }

    public AppSettings Load()
    {
        try
        {
            if (File.Exists(_path))
            {
                return JsonSerializer.Deserialize<AppSettings>(
                    File.ReadAllText(_path))
                    ?? new AppSettings();
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine(
                "[XSphere] Could not load settings: " + ex.Message);
        }

        return new AppSettings();
    }

    public void Save(AppSettings settings)
    {
        var folder = Path.GetDirectoryName(_path)!;
        Directory.CreateDirectory(folder);

        var json = JsonSerializer.Serialize(
            settings,
            new JsonSerializerOptions { WriteIndented = true });

        var tempPath = _path + ".tmp";

        File.WriteAllText(tempPath, json);
        File.Move(tempPath, _path, overwrite: true);
    }
}
