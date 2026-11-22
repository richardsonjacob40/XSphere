
from pathlib import Path
import re
import shutil

path = Path("src/XSphere/MainWindow.cs")
source = path.read_text()

marker = "// CONSOLE TABS FEATURE"

if marker in source:
    raise SystemExit("Console tabs already installed. No changes made.")

# Verify the existing library structure before changing anything.
required = [
    "private Control BuildLibraryPage(bool favoritesOnly)",
    "private void RenderGameCards(bool favoritesOnly, TextBlock? countLabel = null)",
    "var toolbar = new StackPanel",
    "page.Children.Add(toolbar);",
    "var visible = _games.Where(g => !favoritesOnly || g.IsFavorite)"
]

for item in required:
    if item not in source:
        raise SystemExit(f"Missing expected code: {item}")

# Add persistent selection state.
anchor = 'private string _currentPage = "Library";'

if source.count(anchor) != 1:
    raise SystemExit("Could not locate navigation state.")

source = source.replace(
    anchor,
    anchor + '\n'
    '    // CONSOLE TABS FEATURE\n'
    '    private string _selectedConsole = "All Games";',
    1
)

# Work only inside BuildLibraryPage.
start = source.index(
    "private Control BuildLibraryPage(bool favoritesOnly)"
)
end = source.index(
    "private void RenderGameCards(", start
)

library = source[start:end]

# Insert tabs immediately after the existing toolbar.
toolbar_anchor = "page.Children.Add(toolbar);"

if library.count(toolbar_anchor) != 1:
    raise SystemExit("Could not uniquely locate library toolbar.")

tabs = """
        // Console category tabs
        if (!favoritesOnly)
        {
            var consoleTabs = new WrapPanel
            {
                Orientation = Orientation.Horizontal
            };

            var systems = _games
                .Select(g => g.System)
                .Distinct()
                .OrderBy(s => s)
                .ToList();

            systems.Insert(0, "All Games");

            foreach (var system in systems)
            {
                var selected = _selectedConsole == system;

                var tab = MakeButton(
                    system,
                    selected ? AccentBrush : CardBrush,
                    selected ? Brush("#10131B") : MainTextBrush);

                tab.Margin = new Thickness(0, 0, 10, 10);
                tab.MinWidth = 95;
                tab.Height = 42;

                tab.Click += (_, _) =>
                {
                    _selectedConsole = system;
                    RenderCurrentPage();
                };

                consoleTabs.Children.Add(tab);
            }

            page.Children.Add(
                Text("BROWSE BY CONSOLE", 12, MutedBrush, true));

            page.Children.Add(consoleTabs);
        }
"""

library = library.replace(
    toolbar_anchor,
    toolbar_anchor + "\n" + tabs,
    1
)

source = source[:start] + library + source[end:]

# Update game filtering, preserving search and favorites.
start = source.index(
    "private void RenderGameCards("
)
end = source.index(
    "private Control BuildGameCard(Game game)", start
)

render = source[start:end]

old_filter = (
    "var visible = _games.Where(g => !favoritesOnly || g.IsFavorite)"
)

new_filter = """var visible = _games
            .Where(g => !favoritesOnly || g.IsFavorite)
            .Where(g => favoritesOnly
                || _selectedConsole == "All Games"
                || g.System == _selectedConsole)"""

if render.count(old_filter) != 1:
    raise SystemExit("Could not uniquely locate game filter.")

render = render.replace(old_filter, new_filter, 1)

source = source[:start] + render + source[end:]

# Back up before saving.
backup = path.with_name(
    "MainWindow.cs.pre-console-tabs-script"
)
shutil.copy2(path, backup)

path.write_text(source)

print("Console tabs installed successfully.")
print("Backup:", backup)
