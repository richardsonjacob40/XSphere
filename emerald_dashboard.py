
from pathlib import Path
import shutil

path = Path("src/XSphere/MainWindow.cs")
source = path.read_text()

start_marker = "    private Control BuildHomePage()"
end_marker = "    private Control BuildLibraryPage(bool favoritesOnly)"

if source.count(start_marker) != 1 or source.count(end_marker) != 1:
    raise SystemExit("Dashboard markers not found uniquely. No changes made.")

start = source.index(start_marker)
end = source.index(end_marker)

if end <= start:
    raise SystemExit("Unexpected method order. No changes made.")

new_home = r'''
    // EMERALD GLASS CONSOLE DASHBOARD
    private Control BuildHomePage()
    {
        var page = new StackPanel
        {
            Spacing = 24
        };

        // Main welcome banner
        var welcomeContent = new StackPanel
        {
            Spacing = 12
        };

        welcomeContent.Children.Add(
            Text("●  XSPHERE CONSOLE", 12, AccentBrush, true));

        welcomeContent.Children.Add(
            Text("Ready to play?", 36, MainTextBrush, true));

        welcomeContent.Children.Add(
            Text(
                "Your entire gaming universe, all in one place.",
                15, MutedBrush));

        var welcomeActions = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 12,
            Margin = new Thickness(0, 14, 0, 0)
        };

        var browse = MakeButton(
            "▶  Browse Library",
            AccentBrush,
            Brush("#090D0B"));

        browse.MinWidth = 165;
        browse.Height = 48;
        browse.Click += (_, _) => Navigate("Library");

        var favorites = MakeButton(
            "★  Favorites",
            CardBrush,
            MainTextBrush);

        favorites.MinWidth = 140;
        favorites.Height = 48;
        favorites.Click += (_, _) => Navigate("Favorites");

        welcomeActions.Children.Add(browse);
        welcomeActions.Children.Add(favorites);
        welcomeContent.Children.Add(welcomeActions);

        page.Children.Add(new Border
        {
            Background = Brush("#192A20"),
            BorderBrush = Brush("#365943"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(26),
            Padding = new Thickness(32),
            Child = welcomeContent
        });

        // Live statistics
        int gameCount = _games.Count;

        int consoleCount = _games
            .Select(g => g.System)
            .Distinct()
            .Count();

        int favoriteCount = _games.Count(g => g.IsFavorite);

        page.Children.Add(
            Text("YOUR COLLECTION", 13, AccentBrush, true));

        var stats = new WrapPanel();

        foreach (var stat in new[]
        {
            ("TOTAL GAMES", gameCount.ToString(), "▦"),
            ("CONSOLES", consoleCount.ToString(), "▣"),
            ("FAVORITES", favoriteCount.ToString(), "★")
        })
        {
            var statContent = new StackPanel
            {
                Spacing = 10
            };

            statContent.Children.Add(
                Text(stat.Item3, 24, AccentBrush, true));

            statContent.Children.Add(
                Text(stat.Item2, 34, MainTextBrush, true));

            statContent.Children.Add(
                Text(stat.Item1, 12, MutedBrush, true));

            stats.Children.Add(new Border
            {
                Width = 205,
                Background = Brush("#1B2921"),
                BorderBrush = Brush("#344A3B"),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(22),
                Padding = new Thickness(22),
                Margin = new Thickness(0, 0, 16, 16),
                Child = statContent
            });
        }

        page.Children.Add(stats);

        // Featured games
        var featuredHeader = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*,Auto")
        };

        featuredHeader.Children.Add(
            Text("FEATURED GAMES", 21, MainTextBrush, true));

        var viewAll = MakeButton(
            "View All Games  →",
            CardBrush,
            MainTextBrush);

        viewAll.Click += (_, _) => Navigate("Library");

        Grid.SetColumn(viewAll, 1);
        featuredHeader.Children.Add(viewAll);

        page.Children.Add(featuredHeader);

        page.Children.Add(
            Text(
                "Choose a cover to view game details and play.",
                13, MutedBrush));

        var featured = new WrapPanel();

        foreach (var game in _games.Take(4))
        {
            featured.Children.Add(BuildGameCard(game));
        }

        if (_games.Count == 0)
        {
            featured.Children.Add(new Border
            {
                Background = PanelBrush,
                CornerRadius = new CornerRadius(20),
                Padding = new Thickness(24),
                Child = Text(
                    "No games found. Add and scan your ROM folders to get started.",
                    14,
                    MutedBrush)
            });
        }

        page.Children.Add(featured);

        return new ScrollViewer
        {
            Content = page,
            VerticalScrollBarVisibility =
                ScrollBarVisibility.Auto
        };
    }

'''

updated = source[:start] + new_home + source[end:]

backup = path.with_name(
    "MainWindow.cs.before-emerald-dashboard-script"
)

shutil.copy2(path, backup)
path.write_text(updated)

print("Emerald Glass Home Dashboard installed.")
print("Backup:", backup)
