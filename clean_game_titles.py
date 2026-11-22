
from pathlib import Path
import shutil

path = Path("src/XSphere/Services/RomScanner.cs")
source = path.read_text()

old = """                    var title = Path.GetFileNameWithoutExtension(file)
                        .Replace('_', ' ')
                        .Replace('.', ' ')
                        .Trim();
                    if (string.IsNullOrWhiteSpace(title)) title = Path.GetFileName(file);"""

new = """                    var title = CleanGameTitle(file);"""

if source.count(old) != 1:
    raise SystemExit(
        "Expected ROM title code not found. No changes made."
    )

helper = r'''
    // GAME TITLE CLEANER
    private static string CleanGameTitle(string filePath)
    {
        var title = Path.GetFileNameWithoutExtension(filePath);

        // Replace common filename separators.
        title = title.Replace('_', ' ').Replace('.', ' ');

        // Remove standard ROM metadata in parentheses.
        title = System.Text.RegularExpressions.Regex.Replace(
            title,
            @"\s*\((?:USA|Europe|Japan|World|Germany|France|Spain|Italy|Australia|Korea|Rev\s*\d+|Revision\s*\d+|En|Fr|De|Es|It|En,Fr,De|En,Fr,De,Es,It)[^)]*\)",
            "",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);

        // Remove common dump codes and metadata in brackets.
        title = System.Text.RegularExpressions.Regex.Replace(
            title,
            @"\s*\[(?:[A-Z0-9]{4,12}|!|b\d*|a\d*|h\d*|f\d*|t\d*)\]",
            "",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);

        // Remove excessive whitespace.
        title = System.Text.RegularExpressions.Regex.Replace(
            title,
            @"\s+",
            " ").Trim();

        return string.IsNullOrWhiteSpace(title)
            ? Path.GetFileNameWithoutExtension(filePath)
            : title;
    }

'''

marker = "    private static string GuessSystem("

if source.count(marker) != 1:
    raise SystemExit(
        "Helper insertion point not found. No changes made."
    )

updated = source.replace(old, new, 1)
updated = updated.replace(marker, helper + marker, 1)

backup = path.with_name(
    "RomScanner.cs.before-title-cleanup-script"
)

shutil.copy2(path, backup)
path.write_text(updated)

print("Game Title Cleaner installed.")
print("Backup:", backup)
