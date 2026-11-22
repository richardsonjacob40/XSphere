
from pathlib import Path
import shutil

path = Path("src/XSphere/MainWindow.cs")
source = path.read_text()

old = """    private static Button MakeButton(string label, IBrush background, IBrush foreground) => new()
    {
        Content = label,
        Background = background,
        Foreground = foreground,
        Padding = new Thickness(13, 9),
        MinHeight = 38,
        FontSize = 12,
        BorderThickness = new Thickness(0)
    };"""

new = """    // EMERALD GLASS BUTTON STYLE
    private static Button MakeButton(
        string label,
        IBrush background,
        IBrush foreground)
    {
        var button = new Button
        {
            Content = label,
            Background = background,
            Foreground = foreground,
            Padding = new Thickness(15, 10),
            MinHeight = 40,
            FontSize = 13,
            CornerRadius = new CornerRadius(16),
            BorderBrush = Brush("#557463"),
            BorderThickness = new Thickness(1),
            HorizontalContentAlignment =
                HorizontalAlignment.Center,
            VerticalContentAlignment =
                VerticalAlignment.Center
        };

        button.PointerEntered += (_, _) =>
        {
            button.BorderBrush = AccentBrush;
        };

        button.PointerExited += (_, _) =>
        {
            button.BorderBrush = Brush("#557463");
        };

        return button;
    }"""

if source.count(old) != 1:
    raise SystemExit(
        "Original MakeButton method not found. "
        "No changes made."
    )

updated = source.replace(old, new, 1)

backup = path.with_name(
    "MainWindow.cs.before-glass-buttons-script"
)

shutil.copy2(path, backup)
path.write_text(updated)

print("Emerald Glass buttons installed.")
print("Backup:", backup)
