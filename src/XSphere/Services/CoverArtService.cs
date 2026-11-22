
using System;
using System.IO;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using XSphere.Models;

namespace XSphere.Services;

public sealed class CoverArtService
{
    private static readonly HttpClient Client = new()
    {
        Timeout = TimeSpan.FromSeconds(12)
    };

    // Limit concurrent artwork downloads to reduce resource usage.
    private static readonly SemaphoreSlim DownloadLimit = new(3, 3);

    private readonly string _cacheDirectory;

    public CoverArtService()
    {
        _cacheDirectory = Path.Combine(
            XSphereDataMigration.XSphereDirectory, "covers");

        Directory.CreateDirectory(_cacheDirectory);
    } 
public async Task SaveCustomCoverAsync(Game game, string sourcePath)
{
    var destination = GetCoverPath(game);

    await using var source = File.OpenRead(sourcePath);
    using var bitmap = new Avalonia.Media.Imaging.Bitmap(source);

    // Convert JPG, PNG, or other supported images
    // into the PNG format used by our artwork cache.
    using var memory = new MemoryStream();
    bitmap.Save(memory);

    await File.WriteAllBytesAsync(
        destination,
        memory.ToArray());
}

     

    public string GetCoverPath(Game game)
    {
        var key = game.System + "|" + game.Title;
        var hash = SHA256.HashData(
            Encoding.UTF8.GetBytes(key));

        return Path.Combine(
            _cacheDirectory,
            Convert.ToHexString(hash).ToLowerInvariant() + ".png");
    }

    public bool HasCover(Game game) =>
        File.Exists(GetCoverPath(game));

    public async Task<bool> DownloadCoverAsync(Game game)
    {
        await DownloadLimit.WaitAsync();

        try
        {
            return await DownloadCoverCoreAsync(game);
        }
        finally
        {
            DownloadLimit.Release();
        }
    }

    private async Task<bool> DownloadCoverCoreAsync(Game game)
    {
        var destination = GetCoverPath(game);

        if (File.Exists(destination))
            return true;

        var repository = game.System switch
        {
            "Game Boy Advance" => "Nintendo_-_Game_Boy_Advance",
            "GameCube" => "Nintendo_-_GameCube",
            "Nintendo 3DS" => "Nintendo_-_Nintendo_3DS",
            "PlayStation 3" => "Sony_-_PlayStation_3",
            "Xbox" => "Microsoft_-_Xbox",
            "Xbox 360" => "Microsoft_-_Xbox_360",
            _ => null
        };

        if (repository is null)
            return false;

        // Try the original title and a version without
        // common region/version tags.
        var cleanTitle = Regex.Replace(
            game.Title,
            @"\s*\([^)]*\)|\s*\[[^]]*\]",
            "").Trim();

        var alternateTitle = cleanTitle;

// Convert "Legend of Zelda, The" to "The Legend of Zelda".
if (alternateTitle.StartsWith(
    "Legend of Zelda, The",
    StringComparison.OrdinalIgnoreCase))
{
    alternateTitle = alternateTitle.Replace(
        "Legend of Zelda, The",
        "The Legend of Zelda");
}

// Remove filename suffixes such as "# GBA" and "xiso".
alternateTitle = Regex.Replace(
    alternateTitle,
    @"\s*(#\s*GBA|xiso)$",
    "",
    RegexOptions.IgnoreCase).Trim();

// Handle the Dragon Ball GT naming variation.
if (alternateTitle.StartsWith(
    "Dragonball GT",
    StringComparison.OrdinalIgnoreCase))
{
    alternateTitle = alternateTitle.Replace(
        "Dragonball GT",
        "Dragon Ball GT");
}
// Remove revision tags while keeping region and language tags.
var noRevisionTitle = Regex.Replace(
    game.Title,
    @"\s*\(Rev\s+\d+\)",
    "",
    RegexOptions.IgnoreCase).Trim();
// Additional title variations for Xbox artwork.
var xboxTitle = Regex.Replace(
    game.Title,
    @"\s+xiso$",
    "",
    RegexOptions.IgnoreCase).Trim();

// Match Libretro's Family Guy artwork filename.
if (xboxTitle.StartsWith(
    "Family Guy - Video Game!",
    StringComparison.OrdinalIgnoreCase))
{
    xboxTitle = xboxTitle.Replace(
        "Family Guy - Video Game!",
        "Family Guy Video Game!");
}
string[] candidates =
[
    game.Title,
    noRevisionTitle,
    cleanTitle,
    cleanTitle + " (USA)",
    cleanTitle + " (Europe)",
    alternateTitle,
    alternateTitle + " (USA)",
    alternateTitle + " (Europe)",
    xboxTitle,
    "Family Guy Video Game! (USA)"
];

        foreach (var title in candidates.Distinct())
        {
            // Preserve the artwork repository's exact filename.
// URL encoding handles spaces and punctuation.
var safeName = title;

            var url =
                $"https://raw.githubusercontent.com/libretro-thumbnails/" +
                $"{repository}/master/Named_Boxarts/" +
                $"{Uri.EscapeDataString(safeName)}.png";

            try
            {
                using var response = await Client.GetAsync(url);

                if (response.StatusCode == HttpStatusCode.NotFound)
                    continue;

                if (!response.IsSuccessStatusCode)
                    continue;

                var bytes = await response.Content.ReadAsByteArrayAsync();

                // Verify the PNG signature before caching.
                byte[] signature = [137, 80, 78, 71, 13, 10, 26, 10];

                if (bytes.Length < 8 ||
                    !bytes.AsSpan(0, 8).SequenceEqual(signature))
                    continue;

                await File.WriteAllBytesAsync(destination, bytes);
                return true;
            }
            catch (HttpRequestException)
            {
                return false;
            }
            catch (TaskCanceledException)
            {
                return false;
            }
        }

        return await DownloadWithFetchArtAsync(game);
    }

    private async Task<bool> DownloadWithFetchArtAsync(Game game)
    {
        var platform = game.System switch
        {
            "Game Boy Advance" => "gba",
            "GameCube" => "gc",
            "Nintendo 3DS" => "3ds",
            "PlayStation 3" => "ps3",
            "Xbox" => "xbox",
            "Xbox 360" => "x360",
            _ => null
        };

        if (platform is null)
            return false;

        var home = Environment.GetFolderPath(
            Environment.SpecialFolder.UserProfile);

        var executable = Path.Combine(
            home, "xsphere-artwork-env", "bin", "fetchart");

        // Preserve existing ArcadeHub artwork environments.
        if (!File.Exists(executable))
            executable = Path.Combine(
                home, "arcadehub-artwork-env", "bin", "fetchart");

        if (!File.Exists(executable))
            return false;

        var title = Regex.Replace(
            game.Title,
            @"\s*\([^)]*\)|\s*\[[^]]*\]",
            "").Trim();

        title = Regex.Replace(
            title,
            @"\s*(xiso|#\s*GBA)$",
            "",
            RegexOptions.IgnoreCase).Trim();

        if (string.IsNullOrWhiteSpace(title))
            return false;

        var temporaryDirectory = Path.Combine(
            Path.GetTempPath(),
            "xsphere-fetchart-" + Guid.NewGuid().ToString("N"));

        Directory.CreateDirectory(temporaryDirectory);

        try
        {
            var startInfo = new ProcessStartInfo(executable)
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };

            foreach (var argument in new[]
            {
                title, platform,
                "--type", "box",
                "--out", temporaryDirectory,
                "--yes"
            })
            {
                startInfo.ArgumentList.Add(argument);
            }

            using var process = new Process
            {
                StartInfo = startInfo
            };

            if (!process.Start())
                return false;

            var outputTask = process.StandardOutput.ReadToEndAsync();
            var errorTask = process.StandardError.ReadToEndAsync();

            using var timeout = new CancellationTokenSource(
                TimeSpan.FromSeconds(120));

            try
            {
                await process.WaitForExitAsync(timeout.Token);
            }
            catch (OperationCanceledException)
            {
                if (!process.HasExited)
                    process.Kill(entireProcessTree: true);

                await process.WaitForExitAsync();
                return false;
            }

            await Task.WhenAll(outputTask, errorTask);

            if (process.ExitCode != 0)
                return false;

            var artwork = Directory
                .EnumerateFiles(temporaryDirectory)
                .FirstOrDefault(file =>
                    file.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase) ||
                    file.EndsWith(".jpeg", StringComparison.OrdinalIgnoreCase) ||
                    file.EndsWith(".png", StringComparison.OrdinalIgnoreCase));

            if (artwork is null)
                return false;

            using var bitmap =
                new Avalonia.Media.Imaging.Bitmap(artwork);

            using var memory = new MemoryStream();
            bitmap.Save(memory);

            await File.WriteAllBytesAsync(
                GetCoverPath(game),
                memory.ToArray());

            return true;
        }
        catch (Exception ex) when (
            ex is IOException ||
            ex is System.ComponentModel.Win32Exception ||
            ex is UnauthorizedAccessException)
        {
            return false;
        }
        finally
        {
            try
            {
                Directory.Delete(temporaryDirectory, recursive: true);
            }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
    }
}
     
