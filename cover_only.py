
from pathlib import Path

path = Path("src/ArcadeHub/MainWindow.cs")
source = path.read_text()

start_marker = "    private Control BuildGameCard(Game game)"
end_marker = "private async Task DownloadGameCoverAsync(Game game)"

if source.count(start_marker) != 1 or source.count(end_marker) != 1:
    raise SystemExit("Cannot locate game card method. No changes made.")

start = source.index(start_marker)
end = source.index(end_marker, start)

card = source[start:end]

if "// COVER ONLY LIBRARY" in card:
    raise SystemExit("Cover-only mode already installed.")

# Replace only the card builder.
# Existing download logic remains untouched.
new_card = """
    // COVER ONLY LIBRARY
    private Control BuildGameCard(Game game)
    {
        var cardWidth = _consoleMode ? 245.0 : 205.0;
        var coverHeight = _consoleMode ? 340.0 : 285.0;

        var art = new Border
        {
            Background = SystemColor(game.System),
            CornerRadius = new CornerRadius(12),
            Child = new StackPanel
            {
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center,
                Spacing = 8,
                Children =
                {
                    Text(SystemGlyph(game.System), 30,
                        MainTextBrush, true),
                    Text(game.System.ToUpperInvariant(), 11,
                        MainTextBrush, true)
                }
            }
        };

        if (!_coverArt.HasCover(game))
            _ = DownloadGameCoverAsync(game);

        var coverPath = _coverArt.GetCoverPath(game);

        Control cover = art;

        if (File.Exists(coverPath))
        {
            try
            {
                cover = new Image
                {
                    Source = new Bitmap(coverPath),
                    Stretch = Stretch.Uniform
                };
            }
            catch
            {
                cover = art;
            }
        }

        var border = new Border
        {
            Width = cardWidth,
            Height = coverHeight,
            Background = Brush("#151D2A"),
            BorderBrush = Brush("#273247"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(12),
            Padding = new Thickness(5),
            Margin = new Thickness(0, 0, 15, 15),
            Child = cover,
            ClipToBounds = true,
            Cursor = new Avalonia.Input.Cursor(
                Avalonia.Input.StandardCursorType.Hand)
        };

        ToolTip.SetTip(border, game.Title);

        border.PointerReleased += (_, e) =>
        {
            if (e.InitialPressMouseButton !=
                Avalonia.Input.MouseButton.Left)
                return;

            OpenGameDetails(game);
            e.Handled = true;
        };

        border.PointerEntered += (_, _) =>
        {
            border.BorderBrush = AccentBrush;
            border.BorderThickness = new Thickness(3);
        };

        border.PointerExited += (_, _) =>
        {
            border.BorderBrush = Brush("#273247");
            border.BorderThickness = new Thickness(1);
        };

        return border;
    }

"""

path.write_text(source[:start] + new_card + source[end:])

print("Cover-only library installed.")
