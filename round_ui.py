
from pathlib import Path

path = Path("src/ArcadeHub/MainWindow.cs")
source = path.read_text()

if "// SOFT ROUNDED UI" in source:
    raise SystemExit("Rounded UI already installed.")

# Change the corner radius on existing interface panels.
# Preserve the game card method separately.
start_marker = "    // COVER ONLY LIBRARY"
end_marker = "private async Task DownloadGameCoverAsync(Game game)"

if start_marker not in source:
    raise SystemExit("Cover-only game card not found.")

if end_marker not in source:
    raise SystemExit("Game cover download method not found.")

start = source.index(start_marker)
end = source.index(end_marker, start)

before = source[:start]
game_card = source[start:end]
after = source[end:]

# Round general UI panels and controls.
for old, new in [
    ("new CornerRadius(10)", "new CornerRadius(18)"),
    ("new CornerRadius(12)", "new CornerRadius(20)"),
    ("new CornerRadius(13)", "new CornerRadius(20)"),
    ("new CornerRadius(14)", "new CornerRadius(22)"),
    ("new CornerRadius(16)", "new CornerRadius(22)"),
    ("new CornerRadius(18)", "new CornerRadius(24)"),
]:
    before = before.replace(old, new)
    after = after.replace(old, new)

# Keep the game card and cover artwork square.
game_card = game_card.replace(
    "CornerRadius = new CornerRadius(12)",
    "CornerRadius = new CornerRadius(0)"
)

# Make the navigation rail itself rounded.
before = before.replace(
    'Background = Brush("#141B29"),\n'
    '            Padding = new Thickness(10, 20),',
    'Background = Brush("#141B29"),\n'
    '            CornerRadius = new CornerRadius(22),\n'
    '            Margin = new Thickness(8),\n'
    '            Padding = new Thickness(10, 20),'
)

# Mark this installation.
source = (
    "// SOFT ROUNDED UI\n"
    + before
    + game_card
    + after
)

path.write_text(source)

print("Rounded UI installed.")
print("Game cards remain square.")
