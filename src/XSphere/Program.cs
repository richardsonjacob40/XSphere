using Avalonia;

namespace XSphere;

internal static class Program
{
    [STAThread]
    public static int Main(string[] args)
    {
        try
        {
            Services.XSphereDataMigration.Migrate();
            BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
            return 0;
        }
        catch (Exception ex)
        {
            var message = $"XSphere could not start.{Environment.NewLine}{ex}";
            Console.Error.WriteLine(message);

            try
            {
                var logFolder = Services.XSphereAppDataPaths.XSphereDirectory;
                Directory.CreateDirectory(logFolder);
                var logPath = Path.Combine(logFolder, "startup.log");
                File.WriteAllText(logPath, message);
                Console.Error.WriteLine($"Startup details saved to: {logPath}");
            }
            catch (Exception logException)
            {
                Console.Error.WriteLine($"Could not write startup log: {logException.Message}");
            }

            return 1;
        }
    }

    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .LogToTrace();
}
