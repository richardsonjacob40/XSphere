
from pathlib import Path
import shutil

path = Path("src/ArcadeHub/MainWindow.cs")
source = path.read_text()

start_marker = "    private Control BuildGameCard(Game game)"
end_marker = "private async Task DownloadGameCoverAsync(Game game)"

if source.count(start_marker) != 1:
    raise SystemExit("Cannot uniquely find BuildGameCard.")

if source.count(end_marker) != 1:
    raise SystemExit("Cannot uniquely find DownloadGameCoverAsync.")

start = source.index(start_marker)
end = source.index(end_marker, start)
card = source[start:end]

if "// CONSOLE LIBRARY CARD V2" in card:
    raise SystemExit("Console cards are already installed.")

# Confirm that this is the expected working version.
required = [
    "// BIG PICTURE GAME CARD UPGRADE",
    "var buttons = new StackPanel",
    "var detailsButton = MakeButton(",
    "var chooseCover = MakeButton(",
    "stack.Children.Add(chooseCover);",
    "// CLICKABLE GAME CARD REPAIR",
]

for item in required:
    if item not in card:
        raise SystemExit(
            f"Expected code missing: {item}\n"
            "No files have been changed."
        )

# Remove the bulky Play/Favorite/Details button section.
buttons_start = card.index(
    "          var buttons = new StackPanel"
)
buttons_end = card.index(
    "var chooseCover = MakeButton(",
    buttons_start
)

card = (
    card[:buttons_start]
    + card[buttons_end:]
)

# Make the cards cleaner and more artwork-focused.
card = card.replace(
    "// BIG PICTURE GAME CARD UPGRADE",
    "// CONSOLE LIBRARY CARD V2",
    1
)

card = card.replace(
    "var cardWidth = _consoleMode ? 300.0 : 260.0;",
    "var cardWidth = _consoleMode ? 280.0 : 230.0;",
    1
)

card = card.replace(
    "var stack = new StackPanel { Spacing = 12 };",
    "var stack = new StackPanel { Spacing = 8 };",
    1
)

card = card.replace(
    "Height = _consoleMode ? 330 : 285,",
    "Height = _consoleMode ? 350 : 300,"
)

card = card.replace(
    "Width = cardWidth - 28,",
    "Width = cardWidth - 20,",
    1
)

card = card.replace(
    "CornerRadius = new CornerRadius(10),",
    "CornerRadius = new CornerRadius(14),",
    1
)

# Add a favorite indicator next to the console name.
old_system = (
    "stack.Children.Add(Text(game.System, 12, MutedBrush));"
)
new_system = (
    'stack.Children.Add(Text('
    'game.IsFavorite ? game.System + "  ★" : game.System, '
    '12, game.IsFavorite ? AccentBrush : MutedBrush));'
)

if card.count(old_system) != 1:
    raise SystemExit("Console label not found.")

card = card.replace(old_system, new_system, 1)

# Retain the existing custom-cover picker and its handler.
card = card.replace(
    '"Choose Cover",',
    '"Change Cover",',
    1
)

card = card.replace(
    "chooseCover.Height = 36;",
    "chooseCover.Height = 30;",
    1
)

# Reduce card padding and remove the heavy filled panel.
card = card.replace(
    "Background = CardBrush,\n"
    '            BorderBrush = Brush("#303A4D"),',
    'Background = Brush("#151D2A"),\n'
    '            BorderBrush = Brush("#273247"),',
    1
)

card = card.replace(
    "Padding = new Thickness(14),",
    "Padding = new Thickness(10),",
    1
)

card = card.replace(
    "Margin = new Thickness(0, 0, 20, 20),",
    "Margin = new Thickness(0, 0, 16, 18),",
    1
)

# Preserve the existing working click-to-details handler.
# Update the hover border's normal color.
card = card.replace(
    'border.BorderBrush = Brush("#303A4D");',
    'border.BorderBrush = Brush("#273247");',
    1
)

backup = path.with_name(
    "MainWindow.cs.before-console-cards-script"
)

shutil.copy2(path, backup)

updated = source[:start] + card + source[end:]
path.write_text(updated)

print("Console-style game cards installed.")
print("Backup:", backup)
print("Click-to-details and cover picker preserved.")
