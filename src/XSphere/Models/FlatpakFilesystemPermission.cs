using System;

namespace XSphere.Models;

public sealed class FlatpakFilesystemPermission
{
    public string Location { get; }
    public bool IsDenied { get; }
    public bool IsReadOnly { get; }
    public string AccessMode { get; }

    public FlatpakFilesystemPermission(string entry)
    {
        if (string.IsNullOrWhiteSpace(entry))
            throw new ArgumentException(
                "Filesystem permission cannot be empty.",
                nameof(entry));

        string value = entry.Trim();

        IsDenied = value.StartsWith(
            "!",
            StringComparison.Ordinal);

        if (IsDenied)
            value = value[1..];

        AccessMode = "rw";

        foreach (string mode in new[] { "ro", "rw", "create" })
        {
            string suffix = ":" + mode;

            if (value.EndsWith(
                suffix,
                StringComparison.OrdinalIgnoreCase))
            {
                AccessMode = mode;
                value = value[..^suffix.Length];
                break;
            }
        }

        Location = value;
        IsReadOnly = AccessMode == "ro";

        if (string.IsNullOrWhiteSpace(Location))
            throw new ArgumentException(
                "Filesystem permission location cannot be empty.",
                nameof(entry));

        if (Location.Contains(':'))
            throw new ArgumentException(
                "Unsupported filesystem permission syntax.",
                nameof(entry));
    }
}
