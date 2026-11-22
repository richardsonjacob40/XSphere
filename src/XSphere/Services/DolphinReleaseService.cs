using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace XSphere.Services;

public sealed class DolphinReleaseService
{
    private static readonly HttpClient Client = new()
    {
        Timeout = TimeSpan.FromSeconds(30)
    };

    public static Uri OfficialDownloadsPage { get; } =
        new("https://dolphin-emu.org/download/");

    public async Task<bool> CheckDownloadsPageAsync(
        CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Get, OfficialDownloadsPage);

        using var response = await Client.SendAsync(
            request,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken);

        return response.IsSuccessStatusCode;
    }
}
