using System;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace XSphere.Services;

public sealed class EmulatorDownloadService
{
    private static readonly HttpClient Client = new()
    {
        Timeout = TimeSpan.FromMinutes(30)
    };

    public async Task DownloadAsync(
        Uri url,
        string destination,
        IProgress<double>? progress = null,
        CancellationToken cancellationToken = default)
    {
        if (url.Scheme != Uri.UriSchemeHttps)
            throw new ArgumentException(
                "Emulator downloads must use HTTPS.", nameof(url));

        string fullPath = Path.GetFullPath(destination);
        string? directory = Path.GetDirectoryName(fullPath);

        if (directory is null)
            throw new InvalidOperationException("Invalid download path.");

        Directory.CreateDirectory(directory);

        string temporaryPath = fullPath + ".partial";

        try
        {
            using var response = await Client.GetAsync(
                url,
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken);

            response.EnsureSuccessStatusCode();

            long? total = response.Content.Headers.ContentLength;

            await using var source =
                await response.Content.ReadAsStreamAsync(cancellationToken);

            await using var target = new FileStream(
                temporaryPath,
                FileMode.Create,
                FileAccess.Write,
                FileShare.None,
                81920,
                useAsync: true);

            byte[] buffer = new byte[81920];
            long downloaded = 0;
            int read;

            while ((read = await source.ReadAsync(
                buffer.AsMemory(), cancellationToken)) > 0)
            {
                await target.WriteAsync(
                    buffer.AsMemory(0, read), cancellationToken);

                downloaded += read;

                if (total is > 0)
                    progress?.Report(
                        downloaded * 100.0 / total.Value);
            }

            await target.FlushAsync(cancellationToken);
            target.Close();

            File.Move(temporaryPath, fullPath, overwrite: true);
            progress?.Report(100);
        }
        catch
        {
            if (File.Exists(temporaryPath))
                File.Delete(temporaryPath);

            throw;
        }
    }
}
