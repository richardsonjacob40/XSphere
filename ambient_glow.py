
from pathlib import Path
import shutil

path = Path("src/XSphere/MainWindow.cs")
source = path.read_text()

start_marker = "    // EMERALD GLASS CONSOLE DASHBOARD"
end_marker = "    private Control BuildLibraryPage(bool favoritesOnly)"

if source.count(start_marker) != 1 or source.count(end_marker) != 1:
    raise SystemExit("Dashboard markers not found. No changes made.")

start = source.index(start_marker)
end = source.index(end_marker)

if start >= end:
    raise SystemExit("Unexpected dashboard boundaries. No changes made.")

dashboard = source[start:end]

old_banner = '''        page.Children.Add(new Border
        {
            Background = GlassBrush(215),'''

new_banner = '''        page.Children.Add(new Border
        {
            Background = new LinearGradientBrush
            {
                StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
                EndPoint = new RelativePoint(1, 1, RelativeUnit.Relative),
                GradientStops =
                {
                    new GradientStop(Color.Parse("#244D34"), 0),
                    new GradientStop(Color.Parse("#172B20"), 0.45),
                    new GradientStop(Color.Parse("#101A15"), 1)
                }
            },'''

if dashboard.count(old_banner) != 1:
    raise SystemExit(
        "Expected welcome banner not found. "
        "Check that glass_effects.py was installed. "
        "No changes made."
    )

dashboard = dashboard.replace(old_banner, new_banner, 1)

# Add a subtle glow to the statistic panels.
old_stats = '''                Background = GlassBrush(195),'''

new_stats = '''                Background = new LinearGradientBrush
                {
                    StartPoint = new RelativePoint(
                        0, 0, RelativeUnit.Relative),
                    EndPoint = new RelativePoint(
                        1, 1, RelativeUnit.Relative),
                    GradientStops =
                    {
                        new GradientStop(
                            Color.Parse("#263D2D"), 0),
                        new GradientStop(
                            Color.Parse("#19261E"), 0.55),
                        new GradientStop(
                            Color.Parse("#111A15"), 1)
                    }
                },'''

if dashboard.count(old_stats) != 1:
    raise SystemExit(
        "Expected statistics panel not found. "
        "No changes made."
    )

dashboard = dashboard.replace(old_stats, new_stats, 1)

updated = source[:start] + dashboard + source[end:]

backup = path.with_name(
    "MainWindow.cs.before-ambient-glow-script"
)

shutil.copy2(path, backup)
path.write_text(updated)

print("Emerald ambient lighting installed.")
print("Backup:", backup)
