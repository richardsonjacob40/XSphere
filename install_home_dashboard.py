
from pathlib import Path
from datetime import datetime
import re
import shutil

path = Path("src/ArcadeHub/MainWindow.cs")
source = path.read_text()

# Prevent accidental duplicate installation.
definitions = re.findall(
    r"\bBuildHomePage\s*\(\s*\)\s*\{", source
)

if definitions:
    raise SystemExit(
        "A Home Dashboard method already exists. No changes made."
    )

# Locate the existing library page.
anchor = "private Control BuildLibraryPage(bool favoritesOnly)"

if source.count(anchor) != 1:
    raise SystemExit(
        "Could not uniquely locate BuildLibraryPage. No changes made."
    )

# Locate the existing navigation switches.
if source.count('"Favorites" => "Your favorites",') != 1:
    raise SystemExit(
        "Could not locate the page-title switch. No changes made."
    )

if source.count(
    '"Favorites" => BuildLibraryPage(favoritesOnly: true),'
) != 1:
    raise SystemExit(
        "Could not locate the page-rendering switch. No changes made."
    )

dashboard = """
    private Control BuildHomePage()
    {
        var page = new StackPanel { Spacing = 22 };

        // Welcome banner
        page.Children.Add(new Border
        {
            Background = PanelBrush,
            CornerRadius = new CornerRadius(16),
            Padding = new Thickness(26),
            Child = new StackPanel
            {
                Spacing = 8,
                Children =
                {
                    Text("WELCOME TO ARCADEHUB", 12, AccentBrush, true),
                    Text("Your games. One place.", 28, MainTextBrush, true),
                    Text(
                        "Jump back into your favorites or explore your collection.",
                        13, MutedBrush)
                }
            }
        });

        // Live library statistics
        int gameCount = _games.Count;

        int consoleCount = _games
            .Select(g => g.System)
            .Distinct()
            .Count();

        int favoriteCount = _games.Count(g => g.IsFavorite);

        var stats = new WrapPanel();

        foreach (var stat in new[]
        {
            ("TOTAL GAMES", gameCount.ToString()),
            ("CONSOLES", consoleCount.ToString()),
            ("FAVORITES", favoriteCount.ToString())
        })
        {
            stats.Children.Add(new Border
            {
                Width = 195,
                Background = CardBrush,
                CornerRadius = new CornerRadius(13),
                Padding = new Thickness(20),
                Margin = new Thickness(0, 0, 14, 14),
                Child = new StackPanel
                {
                    Spacing = 8,
                    Children =
                    {
                        Text(stat.Item1, 11, MutedBrush, true),
                        Text(stat.Item2, 30, MainTextBrush, true)
                    }
                }
            });
        }

        page.Children.Add(stats);

        // Quick navigation
        page.Children.Add(
            Text("QUICK ACCESS", 13, MutedBrush, true));

        var actions = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 12
        };

        var browse = MakeButton(
            "Browse All Games", AccentBrush, Brush("#10131B"));

        browse.Click += (_, _) => Navigate("Library");

        var favorites = MakeButton(
            "View Favorites", CardBrush, MainTextBrush);

        favorites.Click += (_, _) => Navigate("Favorites");

        actions.Children.Add(browse);
        actions.Children.Add(favorites);

        page.Children.Add(actions);

        // Reuse the existing game cards.
        page.Children.Add(
            Text("FEATURED GAMES", 20, MainTextBrush, true));

        var featured = new WrapPanel();

        foreach (var game in _games.Take(4))
        {
            featured.Children.Add(BuildGameCard(game));
        }

        if (_games.Count == 0)
        {
            featured.Children.Add(
                Text("No games found. Scan your ROM folders.",
                    13, MutedBrush));
        }

        page.Children.Add(featured);

        return new ScrollViewer
        {
            Content = page,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto
        };
    }

"""

updated = source.replace(
    anchor,
    dashboard + "    " + anchor,
    1
)

updated = updated.replace(
    '"Favorites" => "Your favorites",',
    '"Home" => "Home dashboard",\n'
    '            "Favorites" => "Your favorites",',
    1
)

updated = updated.replace(
    '"Favorites" => BuildLibraryPage(favoritesOnly: true),',
    '"Home" => BuildHomePage(),\n'
    '            "Favorites" => BuildLibraryPage(favoritesOnly: true),',
    1
)

# Ensure the dashboard was inserted exactly once.
if len(re.findall(
    r"\bBuildHomePage\s*\(\s*\)\s*\{", updated
)) != 1:
    raise SystemExit("Validation failed. No changes made.")

# Create a timestamped backup before writing.
backup = path.with_name(
    path.name + ".pre-dashboard-" +
    datetime.now().strftime("%Y%m%d-%H%M%S")
)

shutil.copy2(path, backup)
path.write_text(updated)

print("Home Dashboard installed successfully.")
print(f"Backup saved: {backup}")
print("Dashboard method count: 1")
