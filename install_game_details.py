
from pathlib import Path
import shutil

path = Path("src/XSphere/MainWindow.cs")
source = path.read_text()

marker = "// GAME DETAILS PAGE FEATURE"

if marker in source:
    raise SystemExit("Game Details Page is already installed.")

required = [
    'private string _currentPage = "Library";',
    '"Home" => "Home dashboard",',
    '"Home" => BuildHomePage(),',
    "private Control BuildGameCard(Game game)",
    "private void LaunchGame(Game game)",
    "private Control BuildHomePage()",
]

for item in required:
    if source.count(item) != 1:
        raise SystemExit(
            f"Expected exactly one occurrence: {item}"
        )

# Store the selected game and the page to return to.
source = source.replace(
    'private string _currentPage = "Library";',
    '''private string _currentPage = "Library";

    // GAME DETAILS PAGE FEATURE
    private Game? _selectedGame;
    private string _detailsReturnPage = "Library";''',
    1
)

# Add the new page title.
source = source.replace(
    '"Home" => "Home dashboard",',
    '''"Home" => "Home dashboard",
            "Details" => _selectedGame?.Title ?? "Game details",''',
    1
)

# Add the new page renderer.
source = source.replace(
    '"Home" => BuildHomePage(),',
    '''"Home" => BuildHomePage(),
            "Details" => BuildGameDetailsPage(),''',
    1
)

# Add a Details button to each existing game card.
start = source.index(
    "private Control BuildGameCard(Game game)"
)
end = source.index(
    "private async Task DownloadGameCoverAsync",
    start
)

card = source[start:end]

anchor = "stack.Children.Add(buttons);"

if card.count(anchor) != 1:
    raise SystemExit("Game card button insertion point not found.")

card = card.replace(
    anchor,
    anchor + '''

        var detailsButton = MakeButton(
            "Game Details",
            PanelBrush,
            MainTextBrush);

        detailsButton.Height = 42;
        detailsButton.Click += (_, _) => OpenGameDetails(game);

        stack.Children.Add(detailsButton);''',
    1
)

source = source[:start] + card + source[end:]

# Add the details page before the Home page.
anchor = "private Control BuildHomePage()"

details_code = """
    private void OpenGameDetails(Game game)
    {
        _selectedGame = game;
        _detailsReturnPage =
            _currentPage == "Details" ? "Library" : _currentPage;

        Navigate("Details");
    }

    private Control BuildGameDetailsPage()
    {
        if (_selectedGame is null)
        {
            return Text(
                "No game selected.",
                18,
                MutedBrush);
        }

        var game = _selectedGame;

        var page = new StackPanel
        {
            Spacing = 22
        };

        var back = MakeButton(
            "← Back",
            CardBrush,
            MainTextBrush);

        back.HorizontalAlignment = HorizontalAlignment.Left;

        back.Click += (_, _) =>
            Navigate(_detailsReturnPage);

        page.Children.Add(back);

        // Main details layout
        var layout = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions(
                _consoleMode ? "330,*" : "280,*"),
            ColumnSpacing = 28
        };

        var coverPath = _coverArt.GetCoverPath(game);

        Control cover;

        if (File.Exists(coverPath))
        {
            try
            {
                cover = new Image
                {
                    Source = new Bitmap(coverPath),
                    Width = _consoleMode ? 320 : 270,
                    Height = _consoleMode ? 440 : 370,
                    Stretch = Stretch.Uniform
                };
            }
            catch
            {
                cover = Text(
                    game.System,
                    22,
                    MainTextBrush,
                    true);
            }
        }
        else
        {
            cover = new Border
            {
                Background = SystemColor(game.System),
                CornerRadius = new CornerRadius(16),
                Height = 370,
                Child = Text(
                    game.System,
                    22,
                    MainTextBrush,
                    true)
            };
        }

        layout.Children.Add(cover);

        var information = new StackPanel
        {
            Spacing = 16
        };

        information.Children.Add(
            Text(game.Title, 30, MainTextBrush, true,
                wrapping: TextWrapping.Wrap));

        information.Children.Add(
            Text(game.System, 16, AccentBrush, true));

        information.Children.Add(
            Text("GAME INFORMATION", 13, MutedBrush, true));

        information.Children.Add(
            Text("Platform: " + game.System,
                14, MainTextBrush));

        information.Children.Add(
            Text("ROM File: " +
                Path.GetFileName(game.FilePath),
                13, MutedBrush,
                wrapping: TextWrapping.Wrap));

        var play = MakeButton(
            "▶  PLAY GAME",
            AccentBrush,
            Brush("#10131B"));

        play.Height = 55;
        play.HorizontalAlignment = HorizontalAlignment.Left;

        play.Click += (_, _) => LaunchGame(game);

        information.Children.Add(play);

        var favorite = MakeButton(
            game.IsFavorite
                ? "★ Remove from Favorites"
                : "☆ Add to Favorites",
            CardBrush,
            MainTextBrush);

        favorite.Height = 45;
        favorite.HorizontalAlignment = HorizontalAlignment.Left;

        favorite.Click += (_, _) =>
        {
            game.IsFavorite = !game.IsFavorite;
            _database.SetFavorite(game);
            RefreshData();

            // Keep the details page open.
            _selectedGame = _games.FirstOrDefault(
                g => g.Id == game.Id) ?? game;

            RenderCurrentPage();
        };

        information.Children.Add(favorite);

        Grid.SetColumn(information, 1);
        layout.Children.Add(information);

        page.Children.Add(layout);

        return new ScrollViewer
        {
            Content = page,
            VerticalScrollBarVisibility =
                ScrollBarVisibility.Auto
        };
    }

"""

source = source.replace(
    anchor,
    details_code + "    " + anchor,
    1
)

# Verify exactly one new method was inserted.
if source.count(
    "private Control BuildGameDetailsPage()"
) != 1:
    raise SystemExit("Validation failed.")

backup = path.with_name(
    "MainWindow.cs.pre-game-details-script"
)

shutil.copy2(path, backup)
path.write_text(source)

print("Game Details Page installed successfully.")
print("Backup:", backup)
