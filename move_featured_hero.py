
from pathlib import Path
import shutil

path = Path("src/XSphere/MainWindow.cs")
source = path.read_text()

start_marker = "    // EMERALD GLASS CONSOLE DASHBOARD"
end_marker = "    private Control BuildLibraryPage(bool favoritesOnly)"

if source.count(start_marker) != 1 or source.count(end_marker) != 1:
    raise SystemExit("Dashboard boundaries not found. No changes made.")

start = source.index(start_marker)
end = source.index(end_marker)

dashboard = source[start:end]

# Locate the welcome banner and the statistics section.
welcome_start = "        // Main welcome banner"
stats_start = "        // Live statistics"

# Locate the featured hero and the remaining featured games.
hero_start = "        // FEATURED HERO V1"
featured_start = "        // Featured games"

markers = [
    welcome_start,
    stats_start,
    hero_start,
    featured_start
]

if any(dashboard.count(marker) != 1 for marker in markers):
    raise SystemExit(
        "Expected dashboard sections not found. "
        "No changes made."
    )

welcome_pos = dashboard.index(welcome_start)
stats_pos = dashboard.index(stats_start)
hero_pos = dashboard.index(hero_start)
featured_pos = dashboard.index(featured_start)

if not (welcome_pos < stats_pos < hero_pos < featured_pos):
    raise SystemExit(
        "Unexpected dashboard section order. "
        "No changes made."
    )

# Preserve the method declaration and initial page setup.
header = dashboard[:welcome_pos]

# Preserve existing statistics.
statistics = dashboard[stats_pos:hero_pos]

# Preserve the fully functional featured hero.
hero = dashboard[hero_pos:featured_pos]

# Preserve the smaller featured-games section and method ending.
featured_games = dashboard[featured_pos:]

# New order: Hero -> Statistics -> Featured Games.
# The old welcome banner is removed.
new_dashboard = (
    header
    + hero
    + statistics
    + featured_games
)

updated = source[:start] + new_dashboard + source[end:]

backup = path.with_name(
    "MainWindow.cs.before-hero-reorder-script"
)

shutil.copy2(path, backup)
path.write_text(updated)

print("Featured Game Hero moved to the top.")
print("Ready to play banner removed.")
print("Backup:", backup)
