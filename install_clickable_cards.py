
from pathlib import Path
import shutil

path = Path("src/XSphere/MainWindow.cs")
source = path.read_text()

marker = "// CLICKABLE GAME CARDS FEATURE"

if marker in source:
    raise SystemExit("Clickable game cards are already installed.")

start_marker = "private Control BuildGameCard(Game game)"
end_marker = "private void OpenGameDetails(Game game)"

if source.count(start_marker) != 1:
    raise SystemExit("Could not uniquely locate BuildGameCard.")

if source.count(end_marker) != 1:
    raise SystemExit("Could not uniquely locate OpenGameDetails.")

start = source.index(start_marker)
end = source.index(end_marker, start)

card = source[start:end]

# Remove the separate Game Details button.
old_details = '''        var detailsButton = MakeButton(
            "Game Details",
            PanelBrush,
            MainTextBrush);

        detailsButton.Height = 42;
        detailsButton.Click += (_, _) => OpenGameDetails(game);

        stack.Children.Add(detailsButton);
'''

if card.count(old_details) != 1:
    raise SystemExit(
        "Could not find the existing Game Details button."
    )

card = card.replace(old_details, "", 1)

# Add pointer handling to the outer game card.
anchor = '''        border.PointerEntered += (_, _) =>
'''

if card.count(anchor) != 1:
    raise SystemExit(
        "Could not locate the game card hover handler."
    )

click_handler = '''        // CLICKABLE GAME CARDS FEATURE
        border.PointerReleased += (_, e) =>
        {
            // Only respond to a primary-button click.
            if (e.InitialPressMouseButton !=
                Avalonia.Input.MouseButton.Left)
                return;

            // Buttons retain their own click behavior.
            var sourceControl =
                e.Source as Avalonia.Visual;

            var current = sourceControl;

            while (current is not null && current != border)
            {
                if (current is Button)
                    return;

                current = current.VisualParent;
            }

            OpenGameDetails(game);
            e.Handled = true;
        };

'''

card = card.replace(
    anchor,
    click_handler + anchor,
    1
)

source = source[:start] + card + source[end:]

backup = path.with_name(
    "MainWindow.cs.pre-clickable-cards-script"
)

shutil.copy2(path, backup)
path.write_text(source)

print("Clickable game cards installed.")
print("Backup:", backup)
