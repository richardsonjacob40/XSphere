
from pathlib import Path
import shutil

path = Path("src/ArcadeHub/MainWindow.cs")
source = path.read_text()

start_marker = "    // EMERALD GLASS CONSOLE DASHBOARD"
end_marker = "    private Control BuildLibraryPage(bool favoritesOnly)"

if source.count(start_marker) != 1 or source.count(end_marker) != 1:
    raise SystemExit("Dashboard boundaries not found. No changes made.")

start = source.index(start_marker)
end = source.index(end_marker)

dashboard = source[start:end]

if "FEATURED HERO V1" in dashboard:
    raise SystemExit("Featured Hero is already installed.")

anchor = "        // Featured games"

if dashboard.count(anchor) != 1:
    raise SystemExit("Featured games insertion point not found.")

hero = r'''
        // FEATURED HERO V1
        if (_games.Count > 0)
        {
            var heroGame = _games.FirstOrDefault(g => g.IsFavorite)
                ?? _games.First();

            var heroContent = new Grid
            {
                ColumnDefinitions = new ColumnDefinitions("*,220"),
                ColumnSpacing = 28
            };

            var heroInfo = new StackPanel
            {
                Spacing = 14,
                VerticalAlignment = VerticalAlignment.Center
            };

            heroInfo.Children.Add(
                Text("✦  FEATURED GAME", 12, AccentBrush, true));

            heroInfo.Children.Add(
                Text(
                    heroGame.Title,
                    30,
                    MainTextBrush,
                    true,
                    wrapping: TextWrapping.Wrap));

            heroInfo.Children.Add(
                Text(heroGame.System, 15, AccentBrush, true));

            heroInfo.Children.Add(
                Text(
                    "Your next adventure is ready. "
                    + "Launch the game or explore its details.",
                    14,
                    MutedBrush,
                    wrapping: TextWrapping.Wrap));

            var heroActions = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 12,
                Margin = new Thickness(0, 12, 0, 0)
            };

            var playHero = MakeButton(
                "▶  PLAY NOW",
                AccentBrush,
                Brush("#090D0B"));

            playHero.Height = 48;
            playHero.MinWidth = 145;

            playHero.Click += (_, _) => LaunchGame(heroGame);

            var detailsHero = MakeButton(
                "GAME DETAILS",
                CardBrush,
                MainTextBrush);

            detailsHero.Height = 48;

            detailsHero.Click += (_, _) =>
                OpenGameDetails(heroGame);

            heroActions.Children.Add(playHero);
            heroActions.Children.Add(detailsHero);

            heroInfo.Children.Add(heroActions);

            heroContent.Children.Add(heroInfo);

            var heroCoverPath = _coverArt.GetCoverPath(heroGame);

            Control heroCover;

            if (File.Exists(heroCoverPath))
            {
                try
                {
                    heroCover = new Image
                    {
                        Source = new Bitmap(heroCoverPath),
                        Stretch = Stretch.Uniform,
                        Width = 210,
                        Height = 290
                    };
                }
                catch
                {
                    heroCover = Text(
                        heroGame.System,
                        20,
                        MainTextBrush,
                        true);
                }
            }
            else
            {
                if (!_coverArt.HasCover(heroGame))
                    _ = DownloadGameCoverAsync(heroGame);

                heroCover = new Border
                {
                    Width = 210,
                    Height = 290,
                    Background = SystemColor(heroGame.System),
                    CornerRadius = new CornerRadius(0),
                    Child = Text(
                        heroGame.System,
                        20,
                        MainTextBrush,
                        true)
                };
            }

            Grid.SetColumn(heroCover, 1);
            heroContent.Children.Add(heroCover);

            page.Children.Add(new Border
            {
                Background = new LinearGradientBrush
                {
                    StartPoint = new RelativePoint(
                        0, 0, RelativeUnit.Relative),
                    EndPoint = new RelativePoint(
                        1, 1, RelativeUnit.Relative),
                    GradientStops =
                    {
                        new GradientStop(
                            Color.Parse("#244832"), 0),
                        new GradientStop(
                            Color.Parse("#15271C"), 0.5),
                        new GradientStop(
                            Color.Parse("#0D1510"), 1)
                    }
                },
                BorderBrush = GlassBorderBrush(),
                BorderThickness = new Thickness(1.5),
                CornerRadius = new CornerRadius(26),
                Padding = new Thickness(28),
                Child = heroContent
            });
        }

'''

dashboard = dashboard.replace(anchor, hero + anchor, 1)

updated = source[:start] + dashboard + source[end:]

backup = path.with_name(
    "MainWindow.cs.before-featured-hero-script"
)

shutil.copy2(path, backup)
path.write_text(updated)

print("Featured Game Hero installed.")
print("Backup:", backup)
