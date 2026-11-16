
from pathlib import Path
import shutil

path = Path("src/ArcadeHub/MainWindow.cs")
source = path.read_text()

start_marker = "    // EMERALD GLASS CONSOLE DASHBOARD"
end_marker = "    private Control BuildLibraryPage(bool favoritesOnly)"

if source.count(start_marker) != 1:
    raise SystemExit("Dashboard marker not found. No changes made.")

if source.count(end_marker) != 1:
    raise SystemExit("Library marker not found. No changes made.")

start = source.index(start_marker)
end = source.index(end_marker)

if start >= end:
    raise SystemExit("Unexpected method order. No changes made.")

before = source[:start]
dashboard = source[start:end]
after = source[end:]

# Add a reusable translucent glass brush.
glass_helper = '''
    // EMERALD GLASS MATERIALS
    private static IBrush GlassBrush(byte opacity = 205)
    {
        return new SolidColorBrush(
            Color.FromArgb(opacity, 25, 45, 33));
    }

    private static IBrush GlassBorderBrush()
    {
        return new SolidColorBrush(
            Color.FromArgb(170, 95, 160, 113));
    }

'''

helper_marker = "    private static IBrush SystemColor(string system)"

if "private static IBrush GlassBrush(" not in source:
    if before.count(helper_marker) == 1:
        before = before.replace(
            helper_marker, glass_helper + helper_marker, 1
        )
    elif after.count(helper_marker) == 1:
        after = after.replace(
            helper_marker, glass_helper + helper_marker, 1
        )
    else:
        raise SystemExit(
            "Could not locate helper insertion point. "
            "No changes made."
        )

# Only modify the dashboard, not game cards or other pages.
replacements = {
    'Background = Brush("#192A20")':
        'Background = GlassBrush(215)',

    'Background = Brush("#1B2921")':
        'Background = GlassBrush(195)',

    'BorderBrush = Brush("#365943")':
        'BorderBrush = GlassBorderBrush()',

    'BorderBrush = Brush("#344A3B")':
        'BorderBrush = GlassBorderBrush()',

    'BorderThickness = new Thickness(1),':
        'BorderThickness = new Thickness(1.5),',
}

for old, new in replacements.items():
    if old not in dashboard:
        raise SystemExit(
            f"Expected dashboard style missing: {old}"
        )
    dashboard = dashboard.replace(old, new)

updated = before + dashboard + after

backup = path.with_name(
    "MainWindow.cs.before-glass-effects-script"
)

shutil.copy2(path, backup)
path.write_text(updated)

print("Emerald Glass materials installed.")
print("Backup:", backup)
