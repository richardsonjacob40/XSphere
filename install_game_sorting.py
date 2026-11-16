
from pathlib import Path
import shutil

path = Path("src/ArcadeHub/MainWindow.cs")
source = path.read_text()

marker = "// GAME SORTING FEATURE"

if marker in source:
    raise SystemExit("Sorting is already installed. No changes made.")

# Confirm the expected structure before changing anything.
state_anchor = 'private string _selectedConsole = "All Games";'
library_start = "private Control BuildLibraryPage(bool favoritesOnly)"
render_start = "private void RenderGameCards("
card_start = "private Control BuildGameCard(Game game)"

if source.count(state_anchor) != 1:
    raise SystemExit("Console selection field not found.")

if source.count(library_start) != 1:
    raise SystemExit("Library method not found uniquely.")

if source.count(render_start) != 1:
    raise SystemExit("Rendering method not found uniquely.")

if source.count(card_start) != 1:
    raise SystemExit("Game card method not found uniquely.")

# 1. Add sorting state.
source = source.replace(
    state_anchor,
    state_anchor + '\n'
    '    // GAME SORTING FEATURE\n'
    '    private string _gameSort = "Title (A-Z)";',
    1
)

# 2. Add dropdown to the existing toolbar.
start = source.index(library_start)
end = source.index(render_start, start)

library = source[start:end]

anchor = "page.Children.Add(toolbar);"

if library.count(anchor) != 1:
    raise SystemExit("Toolbar insertion point not unique.")

dropdown = """
        // Sort games without changing the selected console.
        var sortBox = new ComboBox
        {
            Width = 180,
            Height = 40,
            ItemsSource = new[]
            {
                "Title (A-Z)",
                "Title (Z-A)",
                "Console",
                "Favorites First"
            },
            SelectedItem = _gameSort
        };

        sortBox.SelectionChanged += (_, _) =>
        {
            if (sortBox.SelectedItem is string selection)
            {
                _gameSort = selection;
                RenderGameCards(favoritesOnly, _gameCountText);
            }
        };

        toolbar.Children.Add(sortBox);
"""

library = library.replace(
    anchor,
    dropdown + "\n        " + anchor,
    1
)

source = source[:start] + library + source[end:]

# 3. Apply sorting to the visible game list.
start = source.index(render_start)
end = source.index(card_start, start)

render = source[start:end]

old = """.ToList();

        if (countLabel is not null)"""

new = """.ToList();

        visible = _gameSort switch
        {
            "Title (Z-A)" => visible
                .OrderByDescending(g => g.Title)
                .ToList(),

            "Console" => visible
                .OrderBy(g => g.System)
                .ThenBy(g => g.Title)
                .ToList(),

            "Favorites First" => visible
                .OrderByDescending(g => g.IsFavorite)
                .ThenBy(g => g.Title)
                .ToList(),

            _ => visible
                .OrderBy(g => g.Title)
                .ToList()
        };

        if (countLabel is not null)"""

if render.count(old) != 1:
    raise SystemExit("Game list sorting insertion point not found.")

render = render.replace(old, new, 1)

source = source[:start] + render + source[end:]

# Back up the original before writing.
backup = path.with_name("MainWindow.cs.pre-sorting-script")
shutil.copy2(path, backup)
path.write_text(source)

print("Game sorting installed successfully.")
print("Backup:", backup)
