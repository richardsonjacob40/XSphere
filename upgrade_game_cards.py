
from pathlib import Path
import shutil

path = Path("src/ArcadeHub/MainWindow.cs")
source = path.read_text()

# Prevent running the upgrade twice.
marker = "// BIG PICTURE GAME CARD UPGRADE"

if marker in source:
    raise SystemExit("Big Picture upgrade already installed.")

# Verify the existing game-card code.
required = [
    "var cardWidth = _consoleMode ? 248.0 : 204.0;",
    "Height = _consoleMode ? 150 : 128,",
    "Width = cardWidth - 22,",
    "Height = _consoleMode ? 220 : 180,",
    "stack.Children.Add(Text(game.Title, 15,",
    "stack.Children.Add(Text(game.System, 11,",
    'var play = MakeButton("▶ Play",',
    "favorite.Width = 43;",
    "stack.Children.Add(chooseCover);",
    "CornerRadius = new CornerRadius(13),"
]

for item in required:
    if item not in source:
        raise SystemExit(
            f"Expected code not found: {item}\n"
            "No changes made."
        )

# Work only inside BuildGameCard.
start = source.index(
    "private Control BuildGameCard(Game game)"
)

end = source.index(
    "private async Task DownloadGameCoverAsync",
    start
)

before = source[:start]
card = source[start:end]
after = source[end:]

card = card.replace(
    "var cardWidth = _consoleMode ? 248.0 : 204.0;",
    """// BIG PICTURE GAME CARD UPGRADE
        var cardWidth = _consoleMode ? 300.0 : 260.0;"""
)

card = card.replace(
    "var stack = new StackPanel { Spacing = 9 };",
    "var stack = new StackPanel { Spacing = 12 };"
)

card = card.replace(
    "Height = _consoleMode ? 150 : 128,",
    "Height = _consoleMode ? 330 : 285,"
)

card = card.replace(
    "Width = cardWidth - 22,",
    "Width = cardWidth - 28,"
)

card = card.replace(
    "Height = _consoleMode ? 220 : 180,",
    "Height = _consoleMode ? 330 : 285,"
)

card = card.replace(
    "stack.Children.Add(Text(game.Title, 15,",
    "stack.Children.Add(Text(game.Title, 18,"
)

card = card.replace(
    "stack.Children.Add(Text(game.System, 11,",
    "stack.Children.Add(Text(game.System, 12,"
)

card = card.replace(
    'var play = MakeButton("▶ Play", AccentBrush, Brush("#10131B"));',
    '''var play = MakeButton(
            "▶  PLAY GAME",
            AccentBrush,
            Brush("#10131B"));

        play.Height = _consoleMode ? 52 : 46;
        play.MinWidth = _consoleMode ? 195 : 155;'''
)

card = card.replace(
    "favorite.Width = 43;",
    """favorite.Width = 48;
        favorite.Height = _consoleMode ? 52 : 46;"""
)

card = card.replace(
    "stack.Children.Add(chooseCover);",
    """chooseCover.Height = 36;
        stack.Children.Add(chooseCover);"""
)

# Update only the card's outer border.
old_border = """CornerRadius = new CornerRadius(13),
            Padding = new Thickness(11),
            Margin = new Thickness(0, 0, 14, 14),
            Child = stack"""

new_border = """CornerRadius = new CornerRadius(18),
            Padding = new Thickness(14),
            Margin = new Thickness(0, 0, 20, 20),
            Child = stack"""

if old_border not in card:
    raise SystemExit("Card border layout not found.")

card = card.replace(old_border, new_border, 1)

# Add a subtle hover highlight.
old_return = """        return border;
    }"""

new_return = """        border.PointerEntered += (_, _) =>
        {
            border.BorderBrush = AccentBrush;
            border.BorderThickness = new Thickness(2);
        };

        border.PointerExited += (_, _) =>
        {
            border.BorderBrush = Brush("#303A4D");
            border.BorderThickness = new Thickness(1);
        };

        return border;
    }"""

if card.count(old_return) != 1:
    raise SystemExit("Could not uniquely locate card return.")

card = card.replace(old_return, new_return, 1)

updated = before + card + after

# Create a backup before writing.
backup = path.with_name(
    "MainWindow.cs.pre-big-picture-script"
)

shutil.copy2(path, backup)
path.write_text(updated)

print("Big Picture game cards installed.")
print("Backup:", backup)
