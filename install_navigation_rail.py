
from pathlib import Path
import shutil

path = Path("src/ArcadeHub/MainWindow.cs")
source = path.read_text()

marker = "// CONSOLE NAVIGATION RAIL"

if marker in source:
    raise SystemExit("Navigation rail is already installed.")

start_marker = "    private Control BuildSidebar()"
end_marker = "    private Button NavButton("

if source.count(start_marker) != 1:
    raise SystemExit("Could not uniquely locate BuildSidebar.")

if source.count(end_marker) != 1:
    raise SystemExit("Could not uniquely locate NavButton.")

start = source.index(start_marker)
end = source.index(end_marker, start)

old_width = (
    'ColumnDefinitions = new ColumnDefinitions('
    '_consoleMode ? "184,*" : "230,*"),'
)

if source.count(old_width) != 1:
    raise SystemExit("Expected sidebar width not found.")

new_sidebar = """
    // CONSOLE NAVIGATION RAIL
    private Control BuildSidebar()
    {
        var sidebar = new Border
        {
            Background = Brush("#141B29"),
            Padding = new Thickness(10, 20),
            BorderBrush = Brush("#273247"),
            BorderThickness = new Thickness(0, 0, 1, 0)
        };

        var layout = new Grid
        {
            RowDefinitions = new RowDefinitions("Auto,*,Auto")
        };

        var navigation = new StackPanel
        {
            Spacing = 14,
            HorizontalAlignment = HorizontalAlignment.Center
        };

        var logo = Text("AH", 20, AccentBrush, true);
        logo.HorizontalAlignment = HorizontalAlignment.Center;
        logo.Margin = new Thickness(0, 6, 0, 20);
        navigation.Children.Add(logo);

        navigation.Children.Add(
            RailButton("⌂", "Home", "Home"));

        navigation.Children.Add(
            RailButton("▦", "All Games", "Library"));

        navigation.Children.Add(
            RailButton("★", "Favorites", "Favorites"));

        navigation.Children.Add(new Border
        {
            Height = 1,
            Width = 40,
            Background = Brush("#303A4D"),
            Margin = new Thickness(0, 10)
        });

        navigation.Children.Add(
            RailButton("▣", "Emulators", "Emulators"));

        navigation.Children.Add(
            RailButton("⚙", "ROM Folders", "Folders"));

        layout.Children.Add(navigation);

        var footer = Text("0.1", 11, MutedBrush);
        footer.HorizontalAlignment = HorizontalAlignment.Center;
        footer.Margin = new Thickness(0, 0, 0, 10);
        Grid.SetRow(footer, 2);
        layout.Children.Add(footer);

        sidebar.Child = layout;
        return sidebar;
    }

    private Button RailButton(
        string symbol,
        string tooltip,
        string page)
    {
        var selected = _currentPage == page;

        // Keep the active library section highlighted
        // while viewing a game's details.
        if (_currentPage == "Details" &&
            page == _detailsReturnPage)
        {
            selected = true;
        }

        var button = MakeButton(
            symbol,
            selected ? Brush("#30466F") : Brushes.Transparent,
            selected ? MainTextBrush : MutedBrush);

        button.Width = 54;
        button.Height = 54;
        button.FontSize = 25;
        button.HorizontalContentAlignment =
            HorizontalAlignment.Center;
        button.VerticalContentAlignment =
            VerticalAlignment.Center;

        ToolTip.SetTip(button, tooltip);

        button.Click += (_, _) => Navigate(page);

        return button;
    }

"""

source = (
    source[:start]
    + new_sidebar
    + source[end:]
)

source = source.replace(
    old_width,
    'ColumnDefinitions = new ColumnDefinitions("76,*"),',
    1
)

# Make the header less bulky.
source = source.replace(
    'new RowDefinitions("76,*")',
    'new RowDefinitions("68,*")',
    1
)

source = source.replace(
    '_pageTitle.Margin = new Thickness(26, 0, 0, 0);',
    '_pageTitle.Margin = new Thickness(24, 0, 0, 0);',
    1
)

source = source.replace(
    'new Thickness(_consoleMode ? 26 : 30, 24, '
    '_consoleMode ? 26 : 30, 24)',
    'new Thickness(26, 24, 26, 24)',
    1
)

backup = path.with_name(
    "MainWindow.cs.pre-navigation-rail-script"
)

shutil.copy2(path, backup)
path.write_text(source)

print("Console navigation rail installed.")
print("Backup:", backup)
