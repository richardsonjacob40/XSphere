
from pathlib import Path
import shutil

path = Path("src/ArcadeHub/MainWindow.cs")
source = path.read_text()

old_theme = """    private static readonly IBrush BackgroundBrush = Brush("#10131B");
    private static readonly IBrush PanelBrush = Brush("#191F2C");
    private static readonly IBrush CardBrush = Brush("#202838");
    private static readonly IBrush AccentBrush = Brush("#7C9CFF");
    private static readonly IBrush MutedBrush = Brush("#AAB4C7");
    private static readonly IBrush MainTextBrush = Brush("#F3F6FC");"""

new_theme = """    // EMERALD GLASS THEME
    private static readonly IBrush BackgroundBrush = Brush("#090D0B");
    private static readonly IBrush PanelBrush = Brush("#17211D");
    private static readonly IBrush CardBrush = Brush("#25322B");
    private static readonly IBrush AccentBrush = Brush("#43E68D");
    private static readonly IBrush MutedBrush = Brush("#9AAEA2");
    private static readonly IBrush MainTextBrush = Brush("#F1F7F3");"""

if source.count(old_theme) != 1:
    raise SystemExit(
        "Expected theme definitions not found. "
        "No changes made."
    )

updated = source.replace(old_theme, new_theme, 1)

# Update remaining blue-themed surfaces and highlights.
colors = {
    "#141B29": "#111A15",
    "#151D2A": "#15221B",
    "#151D2C": "#15221B",
    "#273247": "#344A3B",
    "#303A4D": "#344A3B",
    "#30466F": "#245C3B",
    "#2C3D61": "#245C3B",
    "#7C9CFF": "#43E68D",
    "#10131B": "#090D0B",
}

for old, new in colors.items():
    updated = updated.replace(old, new)

backup = path.with_name(
    "MainWindow.cs.before-emerald-script"
)

shutil.copy2(path, backup)
path.write_text(updated)

print("Emerald Glass theme installed.")
print("Backup:", backup)
