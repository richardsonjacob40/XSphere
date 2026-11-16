
from pathlib import Path
import shutil

path = Path("src/ArcadeHub/MainWindow.cs")
source = path.read_text()

start_marker = "        // CLICKABLE GAME CARDS FEATURE"
end_marker = "        border.PointerEntered +="

if source.count(start_marker) != 1:
    raise SystemExit(
        "Clickable card handler not found exactly once."
    )

start = source.index(start_marker)
end = source.index(end_marker, start)

replacement = """        // CLICKABLE GAME CARDS FEATURE
        border.AddHandler(
            Avalonia.Input.InputElement.PointerPressedEvent,
            (_, e) =>
            {
                if (!e.GetCurrentPoint(border)
                      .Properties.IsLeftButtonPressed)
                    return;

                // Don't open details when clicking action buttons.
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

backup = path.with_name(
    "MainWindow.cs.pre-click-fix-script"
)

shutil.copy2(path, backup)
path.write_text(
    source[:start] + replacement + source[end:]
)

print("Game card click handler updated.")
print("Backup:", backup)
