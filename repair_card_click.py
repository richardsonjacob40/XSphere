
from pathlib import Path
import shutil

path = Path("src/XSphere/MainWindow.cs")
source = path.read_text()

start = source.find("private Control BuildGameCard(Game game)")
end = source.find("private async Task DownloadGameCoverAsync", start)

if start == -1 or end == -1:
    raise SystemExit("Could not locate the game card method.")

card = source[start:end]

if "CLICKABLE GAME CARD REPAIR" in card:
    raise SystemExit("Repair already installed.")

if source.count("private void OpenGameDetails(Game game)") != 1:
    raise SystemExit("Game Details navigation method not found.")

anchor = "        border.PointerEntered += (_, _) =>"

if card.count(anchor) != 1:
    raise SystemExit("Could not locate the card hover handler.")

handler = """        // CLICKABLE GAME CARD REPAIR
        border.AddHandler(
            Avalonia.Input.InputElement.PointerReleasedEvent,
            (_, e) =>
            {
                if (e.InitialPressMouseButton !=
                    Avalonia.Input.MouseButton.Left)
                    return;

                // Ignore clicks on Play, Favorite and Choose Cover.
                var current = e.Source as Avalonia.Visual;

                while (current is not null && current != border)
                {
                    if (current is Button)
                        return;

                    current = current.VisualParent;
                }

                OpenGameDetails(game);
                e.Handled = true;
            },
            Avalonia.Interactivity.RoutingStrategies.Bubble,
            handledEventsToo: true);

"""

card = card.replace(anchor, handler + anchor, 1)

backup = path.with_name(
    "MainWindow.cs.pre-card-click-repair"
)

shutil.copy2(path, backup)

path.write_text(
    source[:start] + card + source[end:]
)

print("Game card click handler installed.")
print("Backup:", backup)
