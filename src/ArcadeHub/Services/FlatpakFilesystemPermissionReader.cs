using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ArcadeHub.Models;

namespace ArcadeHub.Services;

public static class FlatpakFilesystemPermissionReader
{
    public static async Task<IReadOnlyList<FlatpakFilesystemPermission>>
        GetAsync(CancellationToken cancellationToken = default)
    {
        return await GetAsync(
            FlatpakService.DolphinAppId,
            cancellationToken);
    }

    public static async Task<IReadOnlyList<FlatpakFilesystemPermission>>
        GetAsync(
            string appId,
            CancellationToken cancellationToken = default)
    {
        string output =
            await FlatpakEffectivePermissions.GetAsync(
                appId,
                cancellationToken);

        var permissions =
            new List<FlatpakFilesystemPermission>();

        foreach (string entry in
            FlatpakFilesystemParser.Parse(output))
        {
            try
            {
                permissions.Add(
                    new FlatpakFilesystemPermission(entry));
            }
            catch (ArgumentException)
            {
                // Ignore malformed entries without discarding
                // the remaining permission information.
            }
        }

        return permissions;
    }
}
