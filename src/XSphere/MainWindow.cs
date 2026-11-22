using System;
using System.Linq;
// SOFT ROUNDED UI
using System.Collections.Generic;
using System.Threading.Tasks;
using Avalonia.Threading;
using Avalonia.Media.Imaging;
using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using XSphere.Models;
using XSphere.Services;
using XSphere.Controls;

namespace XSphere;

public sealed class MainWindow : Window
{
    // XSPHERE COLOR THEMES
    private static string _theme = "Emerald Glass";

    private static string ThemeColor(
        string emerald,
        string blue,
        string red,
        string purple) =>
        _theme switch
        {
            "Midnight Blue" => blue,
            "Crimson Red" => red,
            "Royal Purple" => purple,
            _ => emerald
        };

    private static IBrush BackgroundBrush =>
        Brush(ThemeColor("#090D0B", "#080E1B", "#160B10", "#100A1A"));

    private static IBrush PanelBrush =>
        GlassBrush(ThemeColor("#17211D", "#142137", "#29171E", "#241830"));

    private static IBrush CardBrush =>
        GlassBrush(ThemeColor("#25322B", "#1E3050", "#3A2029", "#302044"));

    private static IBrush AccentBrush =>
        Brush(ThemeColor("#43E68D", "#60A5FA", "#FB7185", "#B388FF"));

    private static IBrush MutedBrush =>
        Brush(ThemeColor("#9AAEA2", "#A3B5D0", "#C9A4AE", "#B9A8CC"));


    // GLASS AND GLOW APPEARANCE
    private static double _glassTransparency = 20;
    private static double _glowIntensity = 35;


    // HOME DASHBOARD APPEARANCE
    private static double _homeGlassTransparency = 30;
    private static double _homeGlowIntensity = 50;

    private static Color HomeGradientColor(string hex)
    {
        var color = Color.Parse(hex);
        var opacity = 1.0 -
            Math.Clamp(_homeGlassTransparency, 0, 100) / 100.0;

        return Color.FromArgb(
            (byte)Math.Round(opacity * 255),
            color.R, color.G, color.B);
    }

    private static IBrush HomeGlassBrush(string hex)
    {
        return new SolidColorBrush(HomeGradientColor(hex));
    }

    private static IBrush HomePanelBrush =>
        HomeGlassBrush(ThemeColor(
            "#17211D", "#142137", "#29171E", "#241830"));

    private static IBrush HomeCardBrush =>
        HomeGlassBrush(ThemeColor(
            "#25322B", "#1E3050", "#3A2029", "#302044"));

    private static BoxShadows HomeGlow()
    {
        var color = Color.Parse(ThemeColor(
            "#43E68D", "#60A5FA", "#FB7185", "#B388FF"));

        var intensity = Math.Clamp(_homeGlowIntensity, 0, 100);

        if (intensity <= 0)
            return default;

        return new BoxShadows(new BoxShadow
        {
            Color = Color.FromArgb(
                (byte)Math.Round(intensity * 0.9),
                color.R, color.G, color.B),
            Blur = 8 + intensity * 0.24,
            Spread = 0
        });
    }

    private static IBrush GlassBrush(string hex)
    {
        var color = Color.Parse(hex);

        var opacity = 1.0 -
            Math.Clamp(_glassTransparency, 0, 100) / 100.0;

        return new SolidColorBrush(
            Color.FromArgb(
                (byte)Math.Round(opacity * 255),
                color.R,
                color.G,
                color.B));
    }

    private static BoxShadows ThemeGlow()
    {
        var color = Color.Parse(ThemeColor(
            "#43E68D", "#60A5FA", "#FB7185", "#B388FF"));

        var intensity = Math.Clamp(_glowIntensity, 0, 100);

        if (intensity <= 0)
            return default;

        return new BoxShadows(new BoxShadow
        {
            Color = Color.FromArgb(
                (byte)Math.Round(intensity * 0.9),
                color.R, color.G, color.B),
            Blur = 8 + intensity * 0.24,
            Spread = 0
        });
    }

    private static IBrush MainTextBrush =>
        Brush(ThemeColor("#F1F7F3", "#F1F7FF", "#FFF1F3", "#F7F1FF"));


    private readonly ArcadeDatabase _database = new();
    private readonly RomScanner _scanner = new();
    private readonly CoverArtService _coverArt = new();
    private readonly HashSet<string> _coverRequests = new();
    private readonly GameLauncher _launcher = new();
    // XSPHERE METADATA V1
    private readonly GameMetadataService _metadataService = new();
    private readonly HashSet<string> _metadataRequests = new();

    private XSphere.Services.ArcadeBackground?
        _backgroundTexture;

    private XSphere.Services.ArcadeBackground?
        FindBackgroundTexture() => _backgroundTexture;

    private readonly ControllerFocusManager _controllerFocus = new();
    private VirtualPinKeypad? _activePinKeypad;
    private VirtualQwertyKeyboard? _activeQwertyKeyboard;
    private Grid _pageHost = new();
    private TextBlock _statusText = new();
    private TextBlock _pageTitle = new();
    private TextBlock _gameCountText = new();
    private WrapPanel _gameCards = new();
    private TextBox? _searchBox;
    private List<Game> _games = [];
    private List<EmulatorProfile> _emulators = [];
    private List<string> _romFolders = [];
    private string _currentPage = "Home";
    private readonly XSphere.Services.AppSettingsService
        _appSettingsService = new();

    private readonly XSphere.Services.AppSettings
        _appSettings = new XSphere.Services.AppSettingsService().Load();

    private UserProfile? _activeProfile;

    // Controller sidebar selection.
    private static readonly string[] ControllerSidebarPages =
    {
        "Home",
        "Library",
        "Favorites",
        "Emulators",
        "Folders",
        "Saves",
        "Settings",
        "SwitchProfile"
    };

    private int _controllerSidebarIndex = 0;

    // Keep references to the current sidebar buttons.
    private readonly Dictionary<string, Button> _controllerSidebarButtons = new();

    private void UpdateControllerSidebarHighlight()
    {
        foreach (var entry in _controllerSidebarButtons)
        {
            bool selected = _activeProfile is not null &&
                ControllerSidebarPages[_controllerSidebarIndex] == entry.Key;

            entry.Value.BorderBrush = selected
                ? Brushes.LimeGreen
                : Brushes.Transparent;

            entry.Value.BorderThickness = selected
                ? new Thickness(2)
                : new Thickness(0);
        }
    }

    private bool _controllerSortPreview;
    private string? _controllerSortOriginal;
    private bool _controllerProfileScreen;
    private bool _controllerPageFocused;

    public void ControllerHandleInput(string input)
    {
        if (_activeQwertyKeyboard is not null)
        {
            _activeQwertyKeyboard.HandleControllerInput(input);
            return;
        }

        if (_activePinKeypad is not null)
        {
            _activePinKeypad.HandleControllerInput(input);
            return;
        }

        Console.WriteLine(
            $"[Controller] Input={input}, Page={_currentPage}, " +
            $"FocusCount={_controllerFocus.Count}, PageFocused={_controllerPageFocused}");

        if (_activeProfile is null && !_controllerProfileScreen)
            return;

        if (_controllerProfileScreen)
        {
            switch (input)
            {
                case "DpadUp":
                case "YNegative":
                    _controllerFocus.MoveDirectional(0, -1);
                    break;

                case "DpadDown":
                case "YPositive":
                    _controllerFocus.MoveDirectional(0, 1);
                    break;

                case "DpadLeft":
                case "XNegative":
                    _controllerFocus.MoveDirectional(-1, 0);
                    break;

                case "DpadRight":
                case "XPositive":
                    _controllerFocus.MoveDirectional(1, 0);
                    break;

                case "A":
                    _controllerFocus.Activate();
                    break;
            }

            return;
        }

        // Give an open dropdown priority over page navigation.
        var activeComboBox = _controllerFocus.SelectedComboBox;

        if (_controllerPageFocused &&
            activeComboBox is not null &&
            activeComboBox.IsDropDownOpen)
        {
            switch (input)
            {
                case "DpadUp":
                case "YNegative":
                    if (activeComboBox.SelectedIndex > 0)
                        activeComboBox.SelectedIndex--;
                    return;

                case "DpadDown":
                case "YPositive":
                    if (activeComboBox.SelectedIndex <
                        activeComboBox.ItemCount - 1)
                        activeComboBox.SelectedIndex++;
                    return;

                case "A":
                    if (activeComboBox.SelectedItem is string confirmedSort)
                    {
                        _gameSort = confirmedSort;
                        _controllerSortPreview = false;
                        activeComboBox.IsDropDownOpen = false;
                        RenderGameCards(
                            _currentPage == "Favorites",
                            _gameCountText);
                    }
                    return;

                case "B":
                    if (_controllerSortOriginal is not null)
                        activeComboBox.SelectedItem =
                            _controllerSortOriginal;

                    _controllerSortPreview = false;
                    activeComboBox.IsDropDownOpen = false;
                    return;

                default:
                    return;
            }
        }

        switch (input)
        {
            case "DpadUp":
            case "YNegative":
                if (_controllerPageFocused)
                {
                    if (_currentPage == "Emulators")
                        _controllerFocus.MoveEmulatorNavigation(0, -1);
                    else
                        _controllerFocus.MoveDirectional(0, -1);
                }
                else
                    ControllerMoveSidebarSelection(-1);
                break;

            case "DpadDown":
            case "YPositive":
                if (_controllerPageFocused)
                {
                    if (_currentPage == "Emulators")
                        _controllerFocus.MoveEmulatorNavigation(0, 1);
                    else
                        _controllerFocus.MoveDirectional(0, 1);
                }
                else
                    ControllerMoveSidebarSelection(1);
                break;

            case "DpadRight":
            case "XPositive":
                if (!_controllerPageFocused && _controllerFocus.Count > 0)
                {
                    _controllerPageFocused = true;
                    _controllerFocus.SelectFirst();
                }
                else if (_controllerPageFocused)
                {
                    if (_currentPage == "Emulators")
                        _controllerFocus.MoveEmulatorNavigation(1, 0);
                    else
                        _controllerFocus.MoveDirectional(1, 0);
                }
                break;

            case "DpadLeft":
            case "XNegative":
                if (_controllerPageFocused &&
                    !(_currentPage == "Emulators"
                        ? _controllerFocus.MoveEmulatorNavigation(-1, 0)
                        : _controllerFocus.MoveDirectional(-1, 0)))
                {
                    _controllerPageFocused = false;
                    _controllerFocus.ClearSelection();
                    UpdateControllerSidebarHighlight();
                }
                break;

            case "A":
                if (_controllerPageFocused)
                    _controllerFocus.Activate();
                else
                    ControllerActivateSidebarSelection();
                break;

            case "B":
                if (_currentPage == "Details")
                    Navigate(_detailsReturnPage);
                else if (_currentPage == "EmulatorEditor")
                {
                    _editingEmulatorProfileId = null;
                    Navigate("Emulators");
                }
                else
                    Navigate("Home");
                break;

            case "LB":
            case "RB":
                ControllerMoveSidebarSelection(input == "LB" ? -1 : 1);
                ControllerActivateSidebarSelection();
                break;

            case "Start":
                TogglePresentationMode();
                break;
        }
    }

    public void ControllerActivateSidebarSelection()
    {
        // Prevent controller navigation before profile authentication.
        if (_activeProfile is null)
            return;

        string page = ControllerSidebarPages[_controllerSidebarIndex];

        Console.WriteLine(
            $"[XSphere] Controller opening page: {page}");

        if (page == "SwitchProfile")
        {
            ShowProfileSelection();
            return;
        }

        Navigate(page);
    }

    public void ControllerMoveSidebarSelection(int direction)
    {
        // Never navigate the dashboard before profile authentication.
        if (_activeProfile is null)
            return;

        if (direction == 0)
            return;

        _controllerSidebarIndex =
            (_controllerSidebarIndex +
             (direction > 0 ? 1 : -1) +
             ControllerSidebarPages.Length)
            % ControllerSidebarPages.Length;

        Console.WriteLine(
            $"[XSphere] Controller selection: " +
            ControllerSidebarPages[_controllerSidebarIndex]);

        UpdateControllerSidebarHighlight();
    }




    // GAME DETAILS PAGE FEATURE
    private Game? _selectedGame;
    private string _detailsReturnPage = "Library";
    // CONSOLE TABS FEATURE
    private string _selectedConsole = "All Games";
    // GAME SORTING FEATURE
    private string _gameSort = "Title (A-Z)";
    private string _statusMessage = "Ready";
    private bool _consoleMode;

    public MainWindow()
{
    _theme = _appSettings.Theme switch
    {
        "Midnight Blue" => "Midnight Blue",
        "Crimson Red" => "Crimson Red",
        "Royal Purple" => "Royal Purple",
        _ => "Emerald Glass"
    };

    _glassTransparency = Math.Clamp(
        _appSettings.GlassTransparency, 0, 100);

    _glowIntensity = Math.Clamp(
        _appSettings.GlowIntensity, 0, 100);


    _homeGlassTransparency = Math.Clamp(
        _appSettings.HomeGlassTransparency, 0, 80);

    _homeGlowIntensity = Math.Clamp(
        _appSettings.HomeGlowIntensity, 0, 100);

    Title = "XSphere";

    // Request a more integrated window frame
   WindowDecorations = Avalonia.Controls.WindowDecorations.None;

    // Keep the rest of your existing constructor.

        // Disable hover tooltips throughout XSphere.
        ToolTip.SetServiceEnabled(this, false);
        Width = 1220;
        Height = 790;
        MinWidth = 900;
        MinHeight = 620;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        Background = BackgroundBrush;
        RefreshData();
        ShowProfileSelection();

        Opened += async (_, _) =>
        {
            if (_startupAnimationPlayed)
                return;

            _startupAnimationPlayed = true;
            await ShowXSphereStartupAsync();
        };

    }


    private bool _startupAnimationPlayed;

    private async Task ShowXSphereStartupAsync()
    {
        var imagePath = System.IO.Path.Combine(
            AppContext.BaseDirectory,
            "Assets", "Startup", "XSphere.png");

        if (!System.IO.File.Exists(imagePath))
        {
            Console.WriteLine("[XSphere] Startup artwork not found.");
            return;
        }

        try
        {
            var splash = new Image
            {
                Source = new Bitmap(imagePath),
                Stretch = Stretch.Uniform,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Stretch,
                Opacity = 0
            };

            var overlay = new Border
            {
                Background = Brush("#090D18"),
                Child = splash
            };

            var originalContent = Content;

            Content = overlay;

            // Fade in over 700 ms.
            for (int i = 0; i <= 20; i++)
            {
                splash.Opacity = i / 20.0;
                await Task.Delay(35);
            }

            // Hold the completed XSphere logo.
            await Task.Delay(1300);

            // Fade out over 700 ms.
            for (int i = 20; i >= 0; i--)
            {
                splash.Opacity = i / 20.0;
                await Task.Delay(35);
            }

            Content = originalContent;

            Console.WriteLine("[XSphere] Startup animation complete.");
        }
        catch (Exception ex)
        {
            Console.WriteLine(
                $"[XSphere] Startup animation error: {ex.Message}");
            ShowProfileSelection();
        }
    }

    private static SolidColorBrush Brush(string color) => new(Color.Parse(color));

    private void RefreshData()
{
    // Load games and automatically select one preferred ROM
    // for each matching game title and console.
    _games = GameGrouping.SelectPreferredGames(
        _database.GetGames());

    _emulators = _database.GetEmulators();
    _romFolders = _database.GetRomFolders();
}


    // Fade in the current page after switching profiles.
    private async System.Threading.Tasks.Task FadeInCurrentPage()
    {
        if (Content is not Control page)
            return;

        page.Opacity = 0;

        var animation = new Avalonia.Animation.Animation
        {
            Duration = TimeSpan.FromMilliseconds(260),
            FillMode = Avalonia.Animation.FillMode.Forward,
            Children =
            {
                new Avalonia.Animation.KeyFrame
                {
                    Cue = new Avalonia.Animation.Cue(0d),
                    Setters =
                    {
                        new Avalonia.Styling.Setter(
                            Visual.OpacityProperty, 0d)
                    }
                },
                new Avalonia.Animation.KeyFrame
                {
                    Cue = new Avalonia.Animation.Cue(1d),
                    Setters =
                    {
                        new Avalonia.Styling.Setter(
                            Visual.OpacityProperty, 1d)
                    }
                }
            }
        };

        await animation.RunAsync(page);
        page.Opacity = 1;
    }

    private Control BuildWindowFrame(Control pageContent, bool titanium = false)
    {
        var frameBackground = titanium
            ? Brush("#171D26")
            : BackgroundBrush;

        var titleBackground = titanium
            ? Brush("#242C37")
            : Brush(ThemeColor(
                "#101813", "#0D1728", "#1D1017", "#170F24"));

        var titleAccent = titanium
            ? Brush("#B8D9F2")
            : AccentBrush;

        var titleText = titanium
            ? Brush("#E8EDF4")
            : MainTextBrush;

        var frame = new Grid
        {
            RowDefinitions = new RowDefinitions("40,*"),
            Background = frameBackground
        };

        var titleBar = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*,44,44"),
            Height = 40,
            Background = titleBackground
        };

        var title = Text("  XSphere", 15, titleAccent, true);
        title.VerticalAlignment = VerticalAlignment.Center;
        titleBar.Children.Add(title);

        // Minimize button
        var minimize = new Button
        {
            Content = "−",
            Width = 44,
            Height = 40,
            Padding = new Thickness(0),
            Background = titleBackground,
            Foreground = titleText,
            FontSize = 22,
            HorizontalContentAlignment = HorizontalAlignment.Center,
            VerticalContentAlignment = VerticalAlignment.Center
        };

        minimize.Click += (_, _) =>
            WindowState = WindowState.Minimized;

        Grid.SetColumn(minimize, 1);
        titleBar.Children.Add(minimize);

        // Close button
        var close = new Button
        {
            Content = "×",
            Width = 44,
            Height = 40,
            Padding = new Thickness(0),
            Background = titleBackground,
            Foreground = titleText,
            FontSize = 23,
            HorizontalContentAlignment = HorizontalAlignment.Center,
            VerticalContentAlignment = VerticalAlignment.Center
        };

        close.Click += (_, _) => Close();

        Grid.SetColumn(close, 2);
        titleBar.Children.Add(close);

        // Drag the window using the title bar.
        titleBar.PointerPressed += (_, e) =>
        {
            if (e.Source is Button)
                return;

            if (e.GetCurrentPoint(titleBar).Properties.IsLeftButtonPressed)
                BeginMoveDrag(e);
        };

        frame.Children.Add(titleBar);

        Grid.SetRow(pageContent, 1);
        frame.Children.Add(pageContent);

        return frame;
    }


    // Exclusive metallic avatar frame for Profile Selection.
    private Control BuildTitaniumAvatar(string avatar, double size)
    {
        var frame = new Border
        {
            Width = size + 12,
            Height = size + 12,
            CornerRadius = new CornerRadius((size + 12) / 2),
            Background = new LinearGradientBrush
            {
                StartPoint = new RelativePoint(
                    0, 0, RelativeUnit.Relative),
                EndPoint = new RelativePoint(
                    1, 1, RelativeUnit.Relative),
                GradientStops =
                {
                    new GradientStop(
                        Color.Parse("#DDE6EF"), 0),
                    new GradientStop(
                        Color.Parse("#758497"), 0.45),
                    new GradientStop(
                        Color.Parse("#303C4B"), 1)
                }
            },
            BorderBrush = Brush("#B8D9F2"),
            BorderThickness = new Thickness(1.5),
            BoxShadow = new BoxShadows(new BoxShadow
            {
                Color = Color.FromArgb(65, 184, 217, 242),
                Blur = 16,
                Spread = 0
            }),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Child = BuildAvatarVisual(avatar, size, Brush("#A7B4C2"))
        };

        return frame;
    }

    // Titanium PIN verification screen.
    private void ShowProfilePinScreen(
        UserProfile profile,
        Action onVerified)
    {
        var titaniumPanel = Brush("#343E4B");
        var titaniumText = Brush("#E8EDF4");
        var titaniumMuted = Brush("#A7B4C2");
        var titaniumAccent = Brush("#B8D9F2");

        var page = new StackPanel
        {
            Spacing = 18,
            Width = 420,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };

        page.Children.Add(BuildTitaniumAvatar(profile.Avatar, 110));

        var heading = Text(
            "Welcome Back, " + profile.Name,
            27, titaniumText, true);

        heading.HorizontalAlignment =
            HorizontalAlignment.Center;

        page.Children.Add(heading);

        var subtitle = Text(
            "Enter your profile PIN to continue",
            14, titaniumMuted);

        subtitle.HorizontalAlignment =
            HorizontalAlignment.Center;

        page.Children.Add(subtitle);

        var pinBox = NewTextBox("Enter PIN");
        pinBox.PasswordChar = '●';
        pinBox.MaxLength = 8;
        page.Children.Add(pinBox);

        var showKeypad = MakeButton(
            "Show PIN Keypad",
            titaniumPanel,
            titaniumText,
            titanium: true);
        page.Children.Add(showKeypad);

        var feedback = Text("", 13, Brush("#FB7185"));
        page.Children.Add(feedback);

        var unlock = MakeButton(
            "Unlock Profile",
            titaniumAccent,
            Brush("#171D26"),
            titanium: true);

        unlock.Height = 48;

        unlock.Click += (_, _) =>
        {
            var pin = pinBox.Text ?? "";

            if (!ProfileSecurityService.IsValidPin(pin))
            {
                feedback.Text = "Enter a valid 4–8 digit PIN.";
                pinBox.Text = "";
                return;
            }

            var result = _database.VerifyProfilePinWithLockout(
                profile.Id, pin);

            pinBox.Text = "";

            if (result == PinVerificationResult.Locked)
            {
                feedback.Text =
                    "Profile locked after too many incorrect attempts. " +
                    "Try again after 5 minutes.";
                return;
            }

            if (result == PinVerificationResult.Incorrect)
            {
                feedback.Text =
                    "Incorrect PIN. Please try again.";
                return;
            }

            if (result == PinVerificationResult.NotFound)
            {
                feedback.Text =
                    "Profile PIN could not be verified.";
                return;
            }

            unlock.IsEnabled = false;
            onVerified();
        };

        page.Children.Add(unlock);

        var cancel = MakeButton(
            "Cancel",
            titaniumPanel,
            titaniumText,
            titanium: true);

        cancel.Height = 44;
        cancel.Click += (_, _) => ShowProfileSelection();
        page.Children.Add(cancel);

        var background = new Grid
        {
            Background = Brush("#171D26")
        };

        background.Children.Add(
            new ProfileAtmosphere
            {
                AccentColor = "#B8D9F2",
                IsHitTestVisible = false
            });

        background.Children.Add(page);

        var keypadOverlay = new Grid
        {
            Background = Brush("#CC090D18"),
            IsVisible = false
        };

        var keypad = new VirtualPinKeypad(
            pinBox,
            () => unlock.RaiseEvent(
                new Avalonia.Interactivity.RoutedEventArgs(
                    Button.ClickEvent)))
        {
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };

        keypadOverlay.Children.Add(keypad);
        background.Children.Add(keypadOverlay);

        void CloseKeypad()
        {
            keypadOverlay.IsVisible = false;
            _activePinKeypad = null;
        }

        void OpenKeypad()
        {
            keypadOverlay.IsVisible = true;
            _activePinKeypad = keypad;
        }

        keypad.CloseRequested += CloseKeypad;
        showKeypad.Click += (_, _) => OpenKeypad();

        Content = BuildWindowFrame(background, titanium: true);

        _activePinKeypad = null;
        _controllerProfileScreen = true;
        _controllerPageFocused = false;
        _controllerFocus.Clear();

        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            _controllerFocus.RegisterButtons(page);
            _controllerFocus.Unregister(pinBox);
            _controllerFocus.Register(pinBox, OpenKeypad);
            _controllerFocus.SelectFirst();
        }, Avalonia.Threading.DispatcherPriority.Loaded);
    }

    private void ShowProfileSelection(
        string? editProfileId = null)
    {
        _activeProfile = null;
        // Exclusive Titanium palette for Profile Selection.
        var titaniumPanel = Brush("#17233F");
        var titaniumCard = Brush("#24365C");
        var titaniumText = Brush("#F2F5FF");
        var titaniumMuted = Brush("#A6B5D8");
        var titaniumAccent = Brush("#80AFFF");
        var titaniumBackground = Brush("#090D18");


        var page = new StackPanel
        {
            Spacing = 20,
            HorizontalAlignment = HorizontalAlignment.Center,
            Width = 850,
            Margin = new Thickness(25)
        };

        var heading = Text("Who's Playing?", 42, titaniumText, true);
        heading.HorizontalAlignment = HorizontalAlignment.Center;
        page.Children.Add(heading);

        var subtitle = Text(
            "Choose a profile or customize your player",
            14, titaniumMuted);
        subtitle.HorizontalAlignment = HorizontalAlignment.Center;
        page.Children.Add(subtitle);

        var profiles = _database.GetUserProfiles();

        UserProfile? editingProfile = null;
        string selectedAvatar = "A01";

        var profileButtons = new WrapPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Center
        };

        page.Children.Add(profileButtons);

        var editor = new Border
        {
            Background = titaniumPanel,
            BorderBrush = titaniumMuted,
            BorderThickness = new Thickness(1),
            BoxShadow = new BoxShadows(new BoxShadow
            {
                Color = Color.FromArgb(65, 184, 217, 242),
                Blur = 24,
                Spread = 0
            }),
            CornerRadius = new CornerRadius(20),
            Padding = new Thickness(22)
        };

        var editorStack = new StackPanel { Spacing = 14 };

        var editorTitle = Text(
            "Create New Profile", 20, titaniumText, true);
        editorStack.Children.Add(editorTitle);

        var nameBox = NewTextBox("Player name");
        editorStack.Children.Add(nameBox);

        // Optional PIN protection for new profiles.
        var pinBox = NewTextBox("PIN (4–8 digits, optional)");
        pinBox.PasswordChar = '●';

        var confirmPinBox = NewTextBox("Confirm PIN");
        confirmPinBox.PasswordChar = '●';

        var pinHeading = Text(
            "Profile Security", 15, titaniumText, true);

        var pinDescription = Text(
            "Leave blank for an unlocked profile.",
            12, titaniumMuted);

        editorStack.Children.Add(pinHeading);
        editorStack.Children.Add(pinDescription);
        editorStack.Children.Add(pinBox);
        editorStack.Children.Add(confirmPinBox);

        editorStack.Children.Add(Text(
            "Choose Your Avatar", 15, titaniumText, true));

        var avatarStatus = Text(
            "Selected avatar: A01", 14, titaniumAccent);
        editorStack.Children.Add(avatarStatus);

        var avatarPicker = new WrapPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Center
        };

        string[] avatars =
        {
            "A01", "A02", "A03", "A04",
            "A05", "A06", "A07", "A08",
            "A09", "A10", "A11", "A12",
            "A13", "A14", "A15", "A16"
        };

        foreach (var avatar in avatars)
        {
            var choice = avatar;
            var button = MakeButton(
                "", titaniumCard, titaniumText, titanium: true);

            button.Content = BuildTitaniumAvatar(choice, 60);
            button.Width = 86;
            button.Height = 86;
            button.Padding = new Thickness(4);
            button.Margin = new Thickness(5);
            ToolTip.SetTip(button, choice);

            button.Click += (_, _) =>
            {
                selectedAvatar = choice;
                avatarStatus.Text = "Selected avatar: " + choice;
            };

            avatarPicker.Children.Add(button);
        }

        editorStack.Children.Add(avatarPicker);

        var actions = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 10
        };

        var createProfileButton = MakeButton(
            "+ Create Profile",
            titaniumAccent,
            BackgroundBrush, titanium: true);

        createProfileButton.Height = 50;
        createProfileButton.Width = 290;
        createProfileButton.HorizontalAlignment =
            HorizontalAlignment.Center;

        var saveButton = MakeButton(
            "Create Profile",
            titaniumAccent,
            BackgroundBrush, titanium: true);

        var cancelButton = MakeButton(
            "Cancel Editing",
            titaniumCard,
            titaniumText, titanium: true);

        cancelButton.IsVisible = false;

        var feedback = Text("", 12, titaniumMuted);

        saveButton.Click += (_, _) =>
        {
            var name = nameBox.Text?.Trim() ?? "";

            if (name.Length == 0 || name.Length > 32)
            {
                feedback.Text =
                    "Enter a player name between 1 and 32 characters.";
                return;
            }

            var pin = pinBox.Text ?? "";
            var confirmation = confirmPinBox.Text ?? "";

            if (editingProfile is null)
            {
                if (pin != confirmation)
                {
                    feedback.Text = "PINs do not match.";
                    return;
                }

                if (pin.Length > 0 &&
                    !ProfileSecurityService.IsValidPin(pin))
                {
                    feedback.Text =
                        "PIN must contain 4 to 8 digits.";
                    return;
                }
            }
            else
            {
                if (pin != confirmation)
                {
                    feedback.Text = "PINs do not match.";
                    return;
                }

                if (pin.Length > 0 &&
                    !ProfileSecurityService.IsValidPin(pin))
                {
                    feedback.Text =
                        "PIN must contain 4 to 8 digits.";
                    return;
                }

                if (editingProfile.HasPin && pin.Length > 0)
                {
                    feedback.Text =
                        "Existing PIN changes require authentication.";
                    return;
                }
            }

            var profile = new UserProfile
            {
                Id = editingProfile?.Id
                    ?? Guid.NewGuid().ToString("N"),
                Name = name,
                Avatar = selectedAvatar
            };

            try
            {
                if (editingProfile is null)
                {
                    _database.CreateUserProfileWithPin(
                        profile,
                        string.IsNullOrEmpty(pin) ? null : pin);
                }
                else
                {
                    _database.SaveUserProfile(profile);

                    // Add PIN protection to an existing unlocked profile.
                    if (!editingProfile.HasPin && pin.Length > 0)
                    {
                        _database.SetProfilePin(profile.Id, pin);
                    }
                }

                ShowProfileSelection();
            }
            catch (Exception ex)
            {
                feedback.Text = "Could not save profile: " + ex.Message;
            }
        };

        cancelButton.Click += (_, _) =>
        {
            editor.IsVisible = false;
            createProfileButton.IsVisible = true;
            editingProfile = null;
            selectedAvatar = "A01";
            nameBox.Text = "";
            avatarStatus.Text = "Selected avatar: A01";
            editorTitle.Text = "Create New Profile";
            saveButton.Content = "Create Profile";
            cancelButton.IsVisible = false;
            feedback.Text = "";
        };

        actions.Children.Add(saveButton);
        actions.Children.Add(cancelButton);
        editorStack.Children.Add(actions);
        editorStack.Children.Add(feedback);

        editor.Child = editorStack;
        editor.IsVisible = false;


        foreach (var profile in profiles)
        {
            var profileCard = new StackPanel
            {
                Spacing = 8,
                Margin = new Thickness(8)
            };

            var select = MakeButton(
                "",
                titaniumCard,
                titaniumText, titanium: true);

            var profileVisual = new StackPanel
            {
                Orientation = Orientation.Vertical,
                Spacing = 10,
                HorizontalAlignment = HorizontalAlignment.Center
            };
            profileVisual.Children.Add(
                BuildAvatarVisual(profile.Avatar, 112, titaniumAccent));

            var profileIdentity = new StackPanel
            {
                Spacing = 6,
                HorizontalAlignment = HorizontalAlignment.Center
            };

            profileIdentity.Children.Add(
                Text(profile.Name, 17, titaniumText, true));

            if (profile.IsAdmin)
            {
                var adminBadge = new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    Spacing = 6,
                    VerticalAlignment = VerticalAlignment.Center
                };

                adminBadge.Children.Add(
                    Text("◆", 13, titaniumAccent, true));

                adminBadge.Children.Add(
                    Text("ADMINISTRATOR",
                        11, titaniumAccent, true));

                profileIdentity.Children.Add(adminBadge);
            }

            profileVisual.Children.Add(profileIdentity);
            select.Content = profileVisual;

            select.Width = 154;
            select.Height = 170;
            select.Padding = new Thickness(8);
            select.FontSize = 18;
            select.Background = Brushes.Transparent;
            select.BorderBrush = Brushes.Transparent;
            select.BorderThickness = new Thickness(0);
            select.CornerRadius = new CornerRadius(24);

            select.Click += async (_, _) =>
            {
                select.IsEnabled = false;

                if (profile.HasPin)
                {
                    ShowProfilePinScreen(profile, () =>
                    {
                        _activeProfile = profile;
                        _currentPage = "Home";
                        RebuildShell();
                        _ = FadeInCurrentPage();
                    });

                    return;
                }

                var fade = new Avalonia.Animation.Animation
                {
                    Duration = TimeSpan.FromMilliseconds(260),
                    FillMode = Avalonia.Animation.FillMode.Forward,
                    Children =
                    {
                        new Avalonia.Animation.KeyFrame
                        {
                            Cue = new Avalonia.Animation.Cue(0d),
                            Setters =
                            {
                                new Avalonia.Styling.Setter(
                                    Visual.OpacityProperty, 1d)
                            }
                        },
                        new Avalonia.Animation.KeyFrame
                        {
                            Cue = new Avalonia.Animation.Cue(1d),
                            Setters =
                            {
                                new Avalonia.Styling.Setter(
                                    Visual.OpacityProperty, 0d)
                            }
                        }
                    }
                };

                await fade.RunAsync(page);

                _activeProfile = profile;
                _currentPage = "Home";
                RebuildShell();

                // Fade in the Home Dashboard after the Titanium screen fades out.
                await FadeInCurrentPage();
            };

            var edit = MakeButton(
                "Edit Profile",
                titaniumPanel,
                titaniumAccent, titanium: true);

            edit.Width = 290;
            edit.Height = 42;

            edit.Click += (_, _) =>
            {
                if (profile.HasPin)
                {
                    ShowProfilePinScreen(profile, () =>
                    {
                        ShowProfileSelection(profile.Id);
                    });
                    return;
                }

                editingProfile = profile;
                editor.IsVisible = true;
                selectedAvatar = profile.Avatar;
                nameBox.Text = profile.Name;
                pinBox.Text = "";
                confirmPinBox.Text = "";
                avatarStatus.Text =
                    "Selected avatar: " + selectedAvatar;
                editorTitle.Text =
                    "Editing " + profile.Name;
                createProfileButton.IsVisible = false;
                saveButton.Content = "Save Changes";
                cancelButton.IsVisible = true;
                feedback.Text = "";
            };

            if (editProfileId == profile.Id)
            {
                editingProfile = profile;
                editor.IsVisible = true;
                selectedAvatar = profile.Avatar;
                nameBox.Text = profile.Name;
                pinBox.Text = "";
                confirmPinBox.Text = "";
                avatarStatus.Text =
                    "Selected avatar: " + selectedAvatar;
                editorTitle.Text =
                    "Editing " + profile.Name;
                createProfileButton.IsVisible = false;
                saveButton.Content = "Save Changes";
                cancelButton.IsVisible = true;
                feedback.Text = "";
            }

            profileCard.Children.Add(select);
            profileButtons.Children.Add(profileCard);
        }

        if (profiles.Count == 0)
        {
            page.Children.Add(Text(
                "No profiles yet. Create your first player below.",
                14, titaniumMuted));
        }

        createProfileButton.Click += (_, _) =>
        {
            editingProfile = null;
            selectedAvatar = "A01";
            nameBox.Text = "";
            avatarStatus.Text = "Selected avatar: A01";
            editorTitle.Text = "Create New Profile";
            saveButton.Content = "Create Profile";
            cancelButton.IsVisible = true;
            feedback.Text = "";
            editor.IsVisible = true;
            createProfileButton.IsVisible = false;
        };

        page.Children.Add(createProfileButton);
        page.Children.Add(editor);

        var background = new Grid
        {
            // Fixed Titanium background, independent of app theme.
            Background = Brush("#171D26")
        };

        // Exclusive Digital Atmosphere background.
        var profileBackgroundTexture =
            new XSphere.Services.ProfileAtmosphere
            {
                AccentColor = "#B8D9F2",
                IsHitTestVisible = false
            };

        // The texture sits behind the profile cards and editor.
        background.Children.Add(profileBackgroundTexture);

        // The scroll viewer fills the window.
        // A grid inside it centers the profile content
        // whenever there is enough available space.
        var centeredContent = new Grid
        {
            MinHeight = 790,
            HorizontalAlignment = HorizontalAlignment.Stretch
        };

        page.HorizontalAlignment = HorizontalAlignment.Center;
        page.VerticalAlignment = VerticalAlignment.Center;

        centeredContent.Children.Add(page);

        var scroll = new ScrollViewer
        {
            Content = centeredContent,
            VerticalScrollBarVisibility =
                ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility =
                ScrollBarVisibility.Auto,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch
        };

        background.Children.Add(scroll);
        Content = BuildWindowFrame(background, titanium: true);

        _controllerProfileScreen = true;
        _controllerPageFocused = false;
        _controllerFocus.Clear();

        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            _controllerFocus.RegisterButtons(page);
            _controllerFocus.SelectFirst();
        }, Avalonia.Threading.DispatcherPriority.Loaded);
    }

    private static Control BuildAvatarVisual(string avatar, double size, IBrush? ringBrush = null)
    {
        var file = System.IO.Path.Combine(
            AppContext.BaseDirectory, "Assets", "Avatars", avatar + ".png");

        Control content;

        if (System.IO.File.Exists(file))
        {
            try
            {
                content = new Image
                {
                    Source = new Bitmap(file),
                    Width = size,
                    Height = size,
                    Stretch = Stretch.UniformToFill
                };

                var circle = new Border
                {
                    Width = size,
                    Height = size,
                    CornerRadius = new CornerRadius(size / 2),
                    ClipToBounds = true,
                    Background = PanelBrush,
                    Child = content
                };

                return new Border
                {
                    Width = size + 4,
                    Height = size + 4,
                    CornerRadius = new CornerRadius((size + 4) / 2),
                    Background = ringBrush ?? AccentBrush,
                    Padding = new Thickness(2),
                    Child = circle
                };
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(
                    "Could not load avatar " + file + ": " + ex.Message);
            }
        }

        return new Border
        {
            Width = size,
            Height = size,
            CornerRadius = new CornerRadius(size / 2),
            Background = CardBrush,
            ClipToBounds = true,
            Child = new TextBlock
            {
                Text = avatar,
                FontSize = size * 0.45,
                Foreground = MainTextBrush,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            }
        };
    }

    private void ApplyGlassEffectsToCurrentPage()
    {
        // Defer the rebuild until the current slider event completes.
        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            RebuildShell();
        }, Avalonia.Threading.DispatcherPriority.Background);
    }

    private void RebuildShell()
    {
        _controllerProfileScreen = false;

        var root = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("76,*"),
            Background = BackgroundBrush
        };


        // Theme-matched decorative background.
        var backgroundTexture =
            new XSphere.Services.ArcadeBackground
            {
                Style = _appSettings.BackgroundStyle,
                Intensity = _appSettings.BackgroundIntensity,
                AccentColor = ThemeColor(
                    "#43E68D",
                    "#60A5FA",
                    "#FB7185",
                    "#B388FF"),
                IsHitTestVisible = false
            };

        _backgroundTexture = backgroundTexture;
        Grid.SetColumnSpan(backgroundTexture, 2);
        root.Children.Add(backgroundTexture);

        _controllerFocus.Clear();
        _controllerSidebarButtons.Clear();
        var sidebar = BuildSidebar();
        root.Children.Add(sidebar);

        var body = new Grid { RowDefinitions = new RowDefinitions("68,*") };
        Grid.SetColumn(body, 1);
        root.Children.Add(body);

        var header = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*,Auto,Auto"),
            Background = PanelBrush,
            Margin = new Thickness(0, 0, 0, 1)
        };
        _pageTitle = Text(_currentPage, 22, MainTextBrush, true);
        _pageTitle.Margin = new Thickness(24, 0, 0, 0);
        _pageTitle.VerticalAlignment = VerticalAlignment.Center;
        header.Children.Add(_pageTitle);

        _statusText = Text(_statusMessage, 12, MutedBrush);
        _statusText.VerticalAlignment = VerticalAlignment.Center;
        _statusText.Margin = new Thickness(12, 0, 18, 0);
        Grid.SetColumn(_statusText, 1);
        header.Children.Add(_statusText);

        var modeButton = MakeButton(_consoleMode ? "Exit Full Screen" : "Full Screen Mode", AccentBrush, BackgroundBrush);
        modeButton.Margin = new Thickness(0, 0, 22, 0);
        modeButton.VerticalAlignment = VerticalAlignment.Center;
        modeButton.Click += (_, _) => TogglePresentationMode();
        Grid.SetColumn(modeButton, 2);
        header.Children.Add(modeButton);

        body.Children.Add(header);
        _pageHost = new Grid { Margin = new Thickness(26, 24, 26, 24) };
        Grid.SetRow(_pageHost, 1);
        body.Children.Add(_pageHost);
        RenderCurrentPage();

        Content = BuildWindowFrame(root);
    }


    // CONSOLE NAVIGATION RAIL
    private Control BuildSidebar()
    {
        var sidebar = new Border
        {
            Background = GlassBrush(ThemeColor("#111A15", "#101A2B", "#201117", "#1A1028")),
            CornerRadius = new CornerRadius(22),
            Margin = new Thickness(8),
            Padding = new Thickness(10, 20),
            BorderBrush = Brush(ThemeColor("#344A3B", "#354A69", "#59313E", "#49345F")),
            BorderThickness = new Thickness(0, 0, 1, 0)
        };

        var layout = new Grid
        {
            RowDefinitions = new RowDefinitions("Auto,*,Auto")
        };

        var navigation = new StackPanel
{
    Spacing = 14,
    HorizontalAlignment = HorizontalAlignment.Stretch
};

        var logo = Text("AH", 20, AccentBrush, true);
        logo.HorizontalAlignment = HorizontalAlignment.Center;
        logo.Margin = new Thickness(0, 6, 0, 20);
        navigation.Children.Add(logo);

        // Circular Switch Profile button
        var switchProfile = new Button
        {
            Width = 48,
            Height = 48,
            Padding = new Thickness(0),
            Background = CardBrush,
            BorderBrush = AccentBrush,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(24),
            HorizontalAlignment = HorizontalAlignment.Center,
            HorizontalContentAlignment = HorizontalAlignment.Center,
            VerticalContentAlignment = VerticalAlignment.Center
        };

        var profileIcon = new Grid
        {
            Width = 32,
            Height = 32
        };

        // User icon: head
        profileIcon.Children.Add(new Avalonia.Controls.Shapes.Ellipse
        {
            Width = 9,
            Height = 9,
            Fill = AccentBrush,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Top,
            Margin = new Thickness(0, 3, 0, 0)
        });

        // User icon: shoulders
        profileIcon.Children.Add(new Avalonia.Controls.Shapes.Ellipse
        {
            Width = 19,
            Height = 11,
            Fill = AccentBrush,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Bottom,
            Margin = new Thickness(0, 0, 0, 4)
        });

        // Switch arrow
        profileIcon.Children.Add(new TextBlock
        {
            Text = "↔",
            FontSize = 15,
            FontWeight = Avalonia.Media.FontWeight.Bold,
            Foreground = MainTextBrush,
            Background = CardBrush,
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Bottom,
            Margin = new Thickness(0, 0, -2, -3)
        });

        switchProfile.Content = profileIcon;

        switchProfile.Click += (_, _) => ShowProfileSelection();
        _controllerSidebarButtons["SwitchProfile"] = switchProfile;



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
            Background = Brush(ThemeColor("#344A3B", "#354A69", "#59313E", "#49345F")),
            Margin = new Thickness(0, 10)
        });

        navigation.Children.Add(
            RailButton("▣", "Emulators", "Emulators"));



        navigation.Children.Add(
            RailButton("💿", "ROM Folders", "Folders"));

        navigation.Children.Add(
            RailButton("▤", "Game Saves", "Saves"));

        navigation.Children.Add(
            RailButton("⚙", "Settings", "Settings"));

        navigation.Children.Add(switchProfile);

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
        bool isActivePage = _currentPage == page ||
            (_currentPage == "Details" && page == _detailsReturnPage);

        var button = MakeButton(
            symbol,
            isActivePage
                ? Brush(ThemeColor("#245C3B", "#244A7B", "#713044", "#59377C"))
                : Brushes.Transparent,
            isActivePage ? MainTextBrush : MutedBrush);

        // CENTERED CONSOLE NAVIGATION BUTTON
button.Width = 48;
button.Height = 48;
button.MinWidth = 48;
button.MinHeight = 48;

button.Padding = new Thickness(0);
button.Margin = new Thickness(0);

button.FontSize = 24;

button.HorizontalAlignment =
    HorizontalAlignment.Center;

button.VerticalAlignment =
    VerticalAlignment.Center;

button.HorizontalContentAlignment =
    HorizontalAlignment.Center;

button.VerticalContentAlignment =
    VerticalAlignment.Center;

        ToolTip.SetTip(button, tooltip);

        // Controller outline is managed by UpdateControllerSidebarHighlight().
        button.BorderBrush = Brushes.Transparent;
        button.BorderThickness = new Thickness(0);

        _controllerSidebarButtons[page] = button;

        button.Click += (_, _) => Navigate(page);

        return button;
    }

    private Button NavButton(string label, string page)
    {
        var button = MakeButton(label, _currentPage == page ? Brush(ThemeColor("#245C3B", "#244A7B", "#713044", "#59377C")) : Brushes.Transparent,
            _currentPage == page ? MainTextBrush : MutedBrush);
        button.HorizontalContentAlignment = HorizontalAlignment.Left;
        button.Click += (_, _) => Navigate(page);
        return button;
    }

    // Fade and slide the current sidebar page into view.
    private async System.Threading.Tasks.Task AnimateSidebarPageAsync(
        Control page)
    {
        var slide = new TranslateTransform(0, 12);

        page.RenderTransform = slide;
        page.Opacity = 0;

        var animation = new Avalonia.Animation.Animation
        {
            Duration = TimeSpan.FromMilliseconds(180),
            FillMode = Avalonia.Animation.FillMode.Forward,
            Children =
            {
                new Avalonia.Animation.KeyFrame
                {
                    Cue = new Avalonia.Animation.Cue(0d),
                    Setters =
                    {
                        new Avalonia.Styling.Setter(
                            Visual.OpacityProperty, 0d),
                        new Avalonia.Styling.Setter(
                            TranslateTransform.YProperty, 12d)
                    }
                },
                new Avalonia.Animation.KeyFrame
                {
                    Cue = new Avalonia.Animation.Cue(1d),
                    Setters =
                    {
                        new Avalonia.Styling.Setter(
                            Visual.OpacityProperty, 1d),
                        new Avalonia.Styling.Setter(
                            TranslateTransform.YProperty, 0d)
                    }
                }
            }
        };

        try
        {
            await animation.RunAsync(page);

            page.Opacity = 1;
            slide.Y = 0;
        }
        catch (System.Threading.Tasks.TaskCanceledException)
        {
            // The user navigated away before the animation finished.
        }
    }

    private void Navigate(string page)
    {
        // Avoid unnecessary rebuilds when selecting the active page.
        if (_currentPage == page)
            return;

        _currentPage = page;
        RebuildShell();

        // Animate only the newly created page content.
        if (_pageHost.Children.Count > 0 &&
            _pageHost.Children[0] is Control pageContent)
        {
            _ = AnimateSidebarPageAsync(pageContent);
        }
    }

    private void TogglePresentationMode()
    {
        _consoleMode = !_consoleMode;
        WindowState = _consoleMode ? WindowState.FullScreen : WindowState.Normal;
        RebuildShell();
    }

    private void RenderCurrentPage()
    {
        _pageHost.Children.Clear();
        _pageTitle.Text = _currentPage switch
        {
            "Home" => "Home dashboard",
            "Details" => _selectedGame?.Title ?? "Game details",
            "Favorites" => "Your favorites",
            "Emulators" => "Emulator manager",
            "EmulatorEditor" => "Emulator profile editor",
            "Folders" => "ROM folders",
            _ => "Your game library"
        };

        Control page = _currentPage switch
        {
            "Home" => BuildHomePage(),
            "Details" => BuildGameDetailsPage(),
            "Favorites" => BuildLibraryPage(favoritesOnly: true),
            "Emulators" => BuildEmulatorsPage(),
            "EmulatorEditor" => BuildEmulatorEditorPage(),
            "Folders" => BuildFoldersPage(),
            "Saves" => BuildSavesPage(),
            "Settings" => BuildSettingsPage(),
            _ => BuildLibraryPage(favoritesOnly: false)
        };
        _pageHost.Children.Add(page);
    }

    
    
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
        var metadata = _database.GetGameMetadata(game.Id);

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
        _controllerFocus.Register(back, () =>
            back.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(
                Button.ClickEvent)));

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
                CornerRadius = new CornerRadius(22),
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
            Text("ABOUT THIS GAME", 13, MutedBrush, true));
        var metadataStatus = Text(
            metadata?.Description ?? "Looking up game information…",
            14, MainTextBrush, wrapping: TextWrapping.Wrap);
        information.Children.Add(metadataStatus);
        var metadataFields = Text(
            FormatMetadata(metadata), 13, MutedBrush,
            wrapping: TextWrapping.Wrap);
        information.Children.Add(metadataFields);
        if (metadata is not null)
            information.Children.Add(Text("Source: " + metadata.SourceUrl,
                12, AccentBrush, wrapping: TextWrapping.Wrap));
        else
            _ = LoadMetadataAsync(game, metadataStatus, metadataFields);

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
            BackgroundBrush);

        play.Height = 55;
        play.HorizontalAlignment = HorizontalAlignment.Left;

        play.Click += (_, _) => LaunchGame(game);

        information.Children.Add(play);
        _controllerFocus.Register(play, () =>
            play.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(
                Button.ClickEvent)));

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
        _controllerFocus.Register(favorite, () =>
            favorite.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(
                Button.ClickEvent)));

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

    private static string FormatMetadata(GameMetadata? data) => data is null
        ? "Release: —  |  Developer: —  |  Publisher: —  |  Genre: —"
        : $"Release: {data.ReleaseDate}\nDeveloper: {data.Developer}\nPublisher: {data.Publisher}\nGenre: {data.Genre}";

    private async System.Threading.Tasks.Task LoadMetadataAsync(
        Game game, TextBlock description, TextBlock fields)
    {
        if (!_metadataRequests.Add(game.Id)) return;
        try
        {
            var result = await _metadataService.FetchAsync(game);
            if (result is not null)
            {
                _database.SaveGameMetadata(game.Id, result);
                description.Text = result.Description;
                fields.Text = FormatMetadata(result);
            }
            else
                description.Text = "No reliable metadata match found for this game.";
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine("Metadata lookup failed: " + ex.Message);
            description.Text = "Metadata unavailable. Check your internet connection.";
        }
        finally { _metadataRequests.Remove(game.Id); }
    }


    // EMERALD GLASS CONSOLE DASHBOARD
    private Control BuildHomePage()
    {
        var page = new StackPanel
        {
            Spacing = 24
        };

        // ROTATING FEATURED HERO
        if (_games.Count > 0)
        {
            var heroGames = _games.ToList();
            var heroIndex = 0;

            Control BuildHero(int index)
            {
                var heroGame = heroGames[index];

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
                BackgroundBrush);

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

            return new Border
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
                            HomeGradientColor(ThemeColor("#244832", "#24476F", "#61283A", "#50316E")), 0),
                        new GradientStop(
                            HomeGradientColor(ThemeColor("#15271C", "#162C48", "#371A28", "#2D1C42")), 0.5),
                        new GradientStop(
                            HomeGradientColor(ThemeColor("#0D1510", "#0C1728", "#200F18", "#1A1028")), 1)
                    }
                },
                BorderBrush = GlassBorderBrush(),
            BoxShadow = HomeGlow(),
                BorderThickness = new Thickness(1.5),
                CornerRadius = new CornerRadius(26),
                Padding = new Thickness(28),
                Child = heroContent
            };
            }

            var heroHost = new ContentControl
            {
                Content = BuildHero(heroIndex)
            };

            page.Children.Add(heroHost);

            // Manual navigation for the Featured Game banner.
            if (heroGames.Count > 1)
            {
                var heroNavigation = new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Spacing = 16
                };

                var previousHero = MakeButton(
                    "❮  PREVIOUS",
                    CardBrush,
                    MainTextBrush);

                var nextHero = MakeButton(
                    "NEXT  ❯",
                    CardBrush,
                    MainTextBrush);

                previousHero.Width = 135;
                nextHero.Width = 135;

                previousHero.Height = 40;
                nextHero.Height = 40;

                previousHero.Click += (_, _) =>
                {
                    heroIndex =
                        (heroIndex - 1 + heroGames.Count) % heroGames.Count;

                    heroHost.Content = BuildHero(heroIndex);
                };

                nextHero.Click += (_, _) =>
                {
                    heroIndex = (heroIndex + 1) % heroGames.Count;

                    heroHost.Content = BuildHero(heroIndex);
                };

                heroNavigation.Children.Add(previousHero);
                heroNavigation.Children.Add(nextHero);

                page.Children.Add(heroNavigation);
            }

            if (heroGames.Count > 1 &&
                _appSettings.AutoRotateFeatured)
            {
                var seconds =
                    _appSettings.FeaturedRotationSeconds;

                if (seconds != 5 &&
                    seconds != 8 &&
                    seconds != 10 &&
                    seconds != 15)
                {
                    seconds = 8;
                }

                var heroTimer = new Avalonia.Threading.DispatcherTimer
                {
                    Interval = TimeSpan.FromSeconds(seconds)
                };

                heroTimer.Tick += (_, _) =>
                {
                    heroIndex = (heroIndex + 1) % heroGames.Count;
                    heroHost.Content = BuildHero(heroIndex);
                };

                heroHost.AttachedToVisualTree += (_, _) =>
                    heroTimer.Start();

                heroHost.DetachedFromVisualTree += (_, _) =>
                    heroTimer.Stop();
            }
        }

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
                Background = new LinearGradientBrush
                {
                    StartPoint = new RelativePoint(
                        0, 0, RelativeUnit.Relative),
                    EndPoint = new RelativePoint(
                        1, 1, RelativeUnit.Relative),
                    GradientStops =
                    {
                        new GradientStop(
                            HomeGradientColor(ThemeColor("#263D2D", "#243D61", "#542B39", "#49315F")), 0),
                        new GradientStop(
                            HomeGradientColor(ThemeColor("#19261E", "#182B43", "#351D28", "#2C1D3D")), 0.55),
                        new GradientStop(
                            HomeGradientColor(ThemeColor("#111A15", "#101A2B", "#201117", "#1A1028")), 1)
                    }
                },
                BorderBrush = GlassBorderBrush(),
            BoxShadow = HomeGlow(),
                BorderThickness = new Thickness(1.5),
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
                Background = HomePanelBrush,
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

    private Control BuildLibraryPage(bool favoritesOnly)
    {
        var page = new StackPanel { Spacing = 18 };
        page.Children.Add(Text(favoritesOnly ? "Keep your most-played games close." : "Every scanned game, organized by system.", 13, MutedBrush));

        var toolbar = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10 };
        _searchBox = new TextBox
        {
            PlaceholderText = "Search games or systems...",
            Width = _consoleMode ? 360 : 310,
            Height = 40,
            VerticalContentAlignment = VerticalAlignment.Center
        };
        _searchBox.TextChanged += (_, _) => RenderGameCards(favoritesOnly, _gameCountText);
        toolbar.Children.Add(_searchBox);
        var searchInput = _searchBox;

        void OpenSearchKeyboard()
        {
            if (_activeQwertyKeyboard is not null)
                return;

            var overlay = new Grid
            {
                Background = Brush("#CC090D18"),
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Stretch
            };

            var keyboard = new VirtualQwertyKeyboard(searchInput);

            overlay.Children.Add(keyboard);

            void CloseKeyboard()
            {
                _activeQwertyKeyboard = null;
                _pageHost.Children.Remove(overlay);
            }

            keyboard.CloseRequested += CloseKeyboard;

            _activeQwertyKeyboard = keyboard;
            _pageHost.Children.Add(overlay);
        }

        _controllerFocus.Register(_searchBox, OpenSearchKeyboard);

        var showKeyboard = MakeButton(
            "Show Keyboard",
            CardBrush,
            MainTextBrush);

        showKeyboard.Height = 40;
        showKeyboard.Click += (_, _) => OpenSearchKeyboard();

        toolbar.Children.Add(showKeyboard);

        _controllerFocus.Register(showKeyboard, () =>
            showKeyboard.RaiseEvent(
                new Avalonia.Interactivity.RoutedEventArgs(
                    Button.ClickEvent)));

        if (!favoritesOnly)
        {
            var addFolder = MakeButton("+ Add ROM Folder", CardBrush, MainTextBrush);
            addFolder.Click += async (_, _) => await AddRomFolderAsync();
            toolbar.Children.Add(addFolder);
            _controllerFocus.Register(addFolder, () =>
                addFolder.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent)));
            var scan = MakeButton("Scan Library", AccentBrush, BackgroundBrush);
            scan.Click += async (_, _) => await ScanAllFoldersAsync();
            toolbar.Children.Add(scan);
            _controllerFocus.Register(scan, () =>
                scan.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent)));
        }
        
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
            if (_controllerSortPreview)
                return;

            if (sortBox.SelectedItem is string selection)
            {
                _gameSort = selection;
                RenderGameCards(favoritesOnly, _gameCountText);
            }
        };

        toolbar.Children.Add(sortBox);
        _controllerFocus.Register(sortBox, () =>
        {
            if (!sortBox.IsDropDownOpen)
            {
                _controllerSortOriginal = _gameSort;
                _controllerSortPreview = true;
                sortBox.IsDropDownOpen = true;
            }
            else
            {
                sortBox.IsDropDownOpen = false;
                _controllerSortPreview = false;
            }
        });

        page.Children.Add(toolbar);

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
                    selected ? BackgroundBrush : MainTextBrush);

                tab.Margin = new Thickness(0, 0, 10, 10);
                tab.MinWidth = 95;
                tab.Height = 42;

                tab.Click += (_, _) =>
                {
                    _selectedConsole = system;
                    RenderCurrentPage();
                };

                consoleTabs.Children.Add(tab);
                _controllerFocus.Register(tab, () =>
                    tab.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent)));
            }

            page.Children.Add(
                Text("BROWSE BY CONSOLE", 12, MutedBrush, true));

            page.Children.Add(consoleTabs);
        }


        _gameCountText = Text("", 12, MutedBrush);
        page.Children.Add(_gameCountText);
        _gameCards = new WrapPanel { Orientation = Orientation.Horizontal };
        page.Children.Add(_gameCards);
        RenderGameCards(favoritesOnly, _gameCountText);
        return new ScrollViewer { Content = page, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
    }

    private void RenderGameCards(bool favoritesOnly, TextBlock? countLabel = null)
    {
        if (_gameCards is null) return;
        // Remove old cards from controller navigation before rebuilding.
        foreach (var oldCard in _gameCards.Children)
        {
            if (oldCard is Control control)
                _controllerFocus.Unregister(control);
        }

        _gameCards.Children.Clear();
        var query = _searchBox?.Text?.Trim() ?? "";
        var visible = _games
            .Where(g => !favoritesOnly || g.IsFavorite)
            .Where(g => favoritesOnly
                || _selectedConsole == "All Games"
                || g.System == _selectedConsole)
            .Where(g => string.IsNullOrWhiteSpace(query)
                || g.Title.Contains(query, StringComparison.OrdinalIgnoreCase)
                || g.System.Contains(query, StringComparison.OrdinalIgnoreCase))
            .ToList();

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

        if (countLabel is not null) countLabel.Text = $"{visible.Count} game(s)  •  {_emulators.Count} emulator profile(s)";

        if (visible.Count == 0)
        {
            var empty = new Border
            {
                Background = PanelBrush,
                CornerRadius = new CornerRadius(22),
                Padding = new Thickness(24),
                Margin = new Thickness(0, 8, 0, 0),
                Child = new StackPanel
                {
                    Spacing = 8,
                    Children =
                    {
                        Text(favoritesOnly ? "No favorites yet" : "Your library is empty", 19, MainTextBrush, true),
                        Text(favoritesOnly ? "Tap the star on a game to add it here." : "Add a ROM folder, then choose Scan Library. Your games will appear here.", 13, MutedBrush)
                    }
                }
            };
            _gameCards.Children.Add(empty);
            return;
        }

        foreach (var game in visible)
            _gameCards.Children.Add(BuildGameCard(game));
    }


    // COVER ONLY LIBRARY
    private Control BuildGameCard(Game game)
    {
        var cardWidth = _consoleMode ? 245.0 : 205.0;
        var coverHeight = _consoleMode ? 340.0 : 285.0;

        var art = new Border
        {
            Background = SystemColor(game.System),
            CornerRadius = new CornerRadius(0),
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
            Background = Brush(ThemeColor("#15221B", "#15253B", "#301923", "#291B3B")),
            BorderBrush = Brush(ThemeColor("#344A3B", "#354A69", "#59313E", "#49345F")),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(0),
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
            border.BorderBrush = Brush(ThemeColor("#344A3B", "#354A69", "#59313E", "#49345F"));
            border.BorderThickness = new Thickness(1);
        };

        _controllerFocus.Register(border, () => OpenGameDetails(game));

        return border;
    }

private async Task DownloadGameCoverAsync(Game game)
{
    var key = game.System + "|" + game.Title;

    if (!_coverRequests.Add(key))
        return;

    try
    {
        var downloaded = await _coverArt.DownloadCoverAsync(game);

        if (downloaded)
        {
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                // Refresh artwork only on pages displaying game cards.
                if (_currentPage == "Library")
                    RenderGameCards(false, _gameCountText);
                else if (_currentPage == "Favorites")
                    RenderGameCards(true, _gameCountText);
            });
        }
    }
    catch (Exception ex)
    {
        Debug.WriteLine($"Cover download failed: {ex.Message}");
    }
}

    private Control BuildEmulatorsPage()
    {
        var page = new StackPanel { Spacing = 16 };

        var canManageEmulators =
            _database.CanProfileManageEmulators(_activeProfile?.Id);

        if (!canManageEmulators)
        {
            page.Children.Add(Text(
                "Read-only access: Your profile cannot manage emulator configurations.",
                13, MutedBrush));
        }
        page.Children.Add(Text("Connect the emulators already on your computer. Each profile is linked to a system.", 13, MutedBrush));

        if (canManageEmulators)
        {
            var createButton = MakeButton(
                "+ Create Emulator Profile",
                AccentBrush,
                BackgroundBrush);

            createButton.HorizontalAlignment =
                HorizontalAlignment.Left;

            createButton.Click += (_, _) =>
            {
                _editingEmulatorProfileId = null;
                Navigate("EmulatorEditor");
            };

            page.Children.Add(createButton);

            if (OperatingSystem.IsLinux() &&
                !EmulatorPlatformService.IsWsl)
            {
                var detectButton = MakeButton(
                    "Auto-Detect Emulators",
                    AccentBrush,
                    BackgroundBrush);

                detectButton.HorizontalAlignment =
                    HorizontalAlignment.Left;

                detectButton.Click += async (_, _) =>
                {
                    detectButton.IsEnabled = false;
                    SetStatus("Scanning installed Linux emulators...");

                    try
                    {
                        int created =
                            await LinuxEmulatorProfileRegistration.RegisterAsync(
                                _database,
                                _activeProfile?.Id);

                        RefreshData();

                        SetStatus(
                            $"Emulator detection complete. Created {created} profile(s).");

                        RenderCurrentPage();
                    }
                    catch (Exception ex)
                    {
                        SetStatus(
                            "Emulator detection failed: " + ex.Message);
                    }
                    finally
                    {
                        detectButton.IsEnabled = true;
                    }
                };

                page.Children.Add(detectButton);

                _controllerFocus.Register(
                    detectButton,
                    () => detectButton.RaiseEvent(
                        new Avalonia.Interactivity.RoutedEventArgs(
                            Button.ClickEvent)));
            }


            _controllerFocus.Register(createButton, () =>
                createButton.RaiseEvent(
                    new Avalonia.Interactivity.RoutedEventArgs(
                        Button.ClickEvent)));
        }

        page.Children.Add(Text($"Configured emulators ({_emulators.Count})", 17, MainTextBrush, true));
        if (_emulators.Count == 0)
            page.Children.Add(Text("No emulator profiles yet. Add one above to enable one-click launching.", 12, MutedBrush));
        foreach (var emulator in _emulators)
        {
            var row = new Grid
            {
                ColumnDefinitions = new ColumnDefinitions("*,Auto"),
                Background = PanelBrush,
                Margin = new Thickness(0, 0, 0, 7)
            };
            var details = new StackPanel { Spacing = 3, Margin = new Thickness(12, 9) };
            details.Children.Add(Text($"{emulator.Name}  •  {emulator.System}", 14, MainTextBrush, true));
            details.Children.Add(Text(emulator.ExecutablePath, 11, MutedBrush, trimming: TextTrimming.CharacterEllipsis));
            row.Children.Add(details);
            var actions = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 8,
                Margin = new Thickness(8),
                VerticalAlignment = VerticalAlignment.Center
            };

            var edit = MakeButton("Edit", AccentBrush, BackgroundBrush);
            edit.Click += (_, _) =>
            {
                _editingEmulatorProfileId = emulator.Id;
                Navigate("EmulatorEditor");
            };
            actions.Children.Add(edit);
            if (canManageEmulators)
                _controllerFocus.Register(edit, () =>
                    edit.RaiseEvent(
                        new Avalonia.Interactivity.RoutedEventArgs(
                            Button.ClickEvent)));

            var remove = MakeButton("Remove", CardBrush, MutedBrush);
            remove.Margin = new Thickness(8);
            remove.VerticalAlignment = VerticalAlignment.Center;
            remove.Click += (_, _) =>
            {
                try
                {
                    _database.DeleteEmulator(
                        emulator.Id, _activeProfile?.Id);

                    RefreshData();
                    SetStatus("Removed emulator: " + emulator.Name);
                    RenderCurrentPage();
                }
                catch (UnauthorizedAccessException ex)
                {
                    SetStatus(ex.Message);
                    RenderCurrentPage();
                }
            };
            actions.Children.Add(remove);
            if (canManageEmulators)
                _controllerFocus.Register(remove, () =>
                    remove.RaiseEvent(
                        new Avalonia.Interactivity.RoutedEventArgs(
                            Button.ClickEvent)));
            Grid.SetColumn(actions, 1);
            if (canManageEmulators)
                row.Children.Add(actions);
            page.Children.Add(row);
        }

        page.Children.Add(Text("Emulator downloads", 17, MainTextBrush, true));
        page.Children.Add(Text("These buttons open the emulator project's website. Review its instructions and install it yourself, then add its executable above.", 12, MutedBrush, wrapping: TextWrapping.Wrap));
        var downloads = new WrapPanel { Orientation = Orientation.Horizontal };
        foreach (var item in EmulatorDownloadCatalog.Emulators)
        {
            var button = MakeButton(item.Name + " ↗", CardBrush, MainTextBrush);
            button.Margin = new Thickness(0, 0, 8, 8);
            button.Click += (_, _) => OpenWebsite(item.OfficialWebsite);
            downloads.Children.Add(button);
            _controllerFocus.Register(button, () =>
                button.RaiseEvent(
                    new Avalonia.Interactivity.RoutedEventArgs(
                        Button.ClickEvent)));
        }
        var dolphinInstall = MakeButton(
            "Install Dolphin Automatically",
            AccentBrush,
            BackgroundBrush);

        dolphinInstall.Margin = new Thickness(0, 8, 0, 8);

        dolphinInstall.Click += async (_, _) =>
        {
            if (!_database.CanProfileManageEmulators(
                _activeProfile?.Id))
            {
                SetStatus(
                    "You do not have permission to install emulators.");
                return;
            }

            bool isWindows = OperatingSystem.IsWindows();
            bool isNativeLinux =
                OperatingSystem.IsLinux() &&
                !EmulatorPlatformService.IsWsl;

            if (!isWindows && !isNativeLinux)
            {
                SetStatus(
                    "Automatic Dolphin installation supports Windows and native Linux only.");
                return;
            }

            if (isNativeLinux &&
                !await FlatpakService.IsAvailableAsync())
            {
                SetStatus(
                    "Flatpak is required for automatic Dolphin installation.");
                return;
            }

            string installationSource = isWindows
                ? "Windows Package Manager (WinGet)"
                : "Flathub";

            bool approved =
                await EmulatorInstallConfirmation.ShowAsync(
                    this,
                    "Dolphin",
                    installationSource);

            if (!approved)
            {
                SetStatus("Dolphin installation cancelled.");
                return;
            }

            dolphinInstall.IsEnabled = false;
            SetStatus($"Installing Dolphin using {installationSource}...");

            try
            {
                var installer = new DolphinInstallService();

                if (isWindows)
                {
                    await installer.InstallOnWindowsAsync(
                        userApproved: true);
                }
                else
                {
                    await installer.InstallOnLinuxAsync(
                        userApproved: true);
                }

                RefreshData();

                int created =
                    await DolphinProfileRegistration.RegisterMissingProfilesAsync(
                        _database,
                        _emulators,
                        _activeProfile?.Id);

                RefreshData();

                SetStatus(
                    $"Dolphin installed successfully. Created {created} emulator profile(s).");

                RenderCurrentPage();
            }
            catch (Exception ex)
            {
                SetStatus(
                    "Dolphin installation failed: " + ex.Message);
            }
            finally
            {
                dolphinInstall.IsEnabled = true;
            }
        };

        downloads.Children.Add(dolphinInstall);

        _controllerFocus.Register(
            dolphinInstall,
            () => dolphinInstall.RaiseEvent(
                new Avalonia.Interactivity.RoutedEventArgs(
                    Button.ClickEvent)));


        var retroArchInstall = MakeButton(
            "Install RetroArch Automatically",
            AccentBrush,
            BackgroundBrush);

        retroArchInstall.Margin = new Thickness(0, 8, 8, 8);

        retroArchInstall.Click += async (_, _) =>
        {
            if (!_database.CanProfileManageEmulators(
                _activeProfile?.Id))
            {
                SetStatus(
                    "You do not have permission to install emulators.");
                return;
            }

            bool isNativeLinux =
                OperatingSystem.IsLinux() &&
                !EmulatorPlatformService.IsWsl;

            if (!isNativeLinux)
            {
                SetStatus(
                    "Automatic RetroArch installation currently supports native Linux only. Use the RetroArch website for other platforms.");
                return;
            }

            if (!await FlatpakService.IsAvailableAsync())
            {
                SetStatus(
                    "Flatpak is required for automatic RetroArch installation.");
                return;
            }

            bool approved =
                await EmulatorInstallConfirmation.ShowAsync(
                    this,
                    "RetroArch",
                    "Flathub");

            if (!approved)
            {
                SetStatus("RetroArch installation cancelled.");
                return;
            }

            retroArchInstall.IsEnabled = false;
            SetStatus("Installing RetroArch using Flathub...");

            try
            {
                var installer = new EmulatorFlatpakInstallService();

                await installer.InstallAsync(
                    "retroarch",
                    userApproved: true);

                bool installed = await FlatpakService.IsAppInstalledAsync(
                    "org.libretro.RetroArch");

                if (!installed)
                    throw new InvalidOperationException(
                        "RetroArch installation could not be verified.");

                SetStatus(
                    "RetroArch installed successfully. Configure its cores before registering game-system profiles. Launch arguments remain editable.");
            }
            catch (Exception ex)
            {
                SetStatus(
                    "RetroArch installation failed: " + ex.Message);
            }
            finally
            {
                retroArchInstall.IsEnabled = true;
            }
        };

        downloads.Children.Add(retroArchInstall);

        _controllerFocus.Register(
            retroArchInstall,
            () => retroArchInstall.RaiseEvent(
                new Avalonia.Interactivity.RoutedEventArgs(
                    Button.ClickEvent)));

        page.Children.Add(downloads);

        return new ScrollViewer { Content = page, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
    }


    private string? _editingEmulatorProfileId;

    private Control BuildEmulatorEditorPage()
    {
        var page = new StackPanel { Spacing = 16 };

        var canManage = _database.CanProfileManageEmulators(
            _activeProfile?.Id);

        if (!canManage)
        {
            page.Children.Add(Text(
                "You do not have permission to manage emulators.",
                15, MutedBrush));

            return page;
        }

        var emulator = _emulators.FirstOrDefault(
            item => item.Id == _editingEmulatorProfileId);

        var editing = emulator is not null;

        page.Children.Add(Text(
            editing ? "Edit Emulator Profile" : "Create Emulator Profile",
            24, MainTextBrush, true));

        var nameBox = NewTextBox("Emulator name (e.g. Dolphin)");
        var systemBox = NewTextBox("System (e.g. GameCube)");
        var pathBox = NewTextBox("Path to emulator executable");
        var argsBox = NewTextBox("Launch arguments");

        nameBox.Text = emulator?.Name ?? "";
        systemBox.Text = emulator?.System ?? "";
        pathBox.Text = emulator?.ExecutablePath ?? "";
        argsBox.Text = emulator?.ArgumentsTemplate ?? "\"{rom}\"";

        var browse = MakeButton(
            "Browse...", CardBrush, MainTextBrush);

        browse.Click += async (_, _) =>
        {
            var files = await StorageProvider.OpenFilePickerAsync(
                new FilePickerOpenOptions
                {
                    Title = "Choose emulator executable",
                    AllowMultiple = false
                });

            if (files.Count > 0)
                pathBox.Text = files[0].Path.LocalPath;
        };

        var pathRow = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*,Auto")
        };

        pathRow.Children.Add(pathBox);
        Grid.SetColumn(browse, 1);
        pathRow.Children.Add(browse);

        page.Children.Add(nameBox);
        page.Children.Add(systemBox);
        page.Children.Add(pathRow);
        page.Children.Add(argsBox);

        page.Children.Add(Text(
            "Use {rom} for the game file path. For RetroArch, use {core} to automatically select an installed core for the game system. You can also enter a core path manually.",
            12, MutedBrush));

        var save = MakeButton(
            editing ? "Save Changes" : "Save Emulator Profile",
            AccentBrush, BackgroundBrush);

        save.HorizontalAlignment = HorizontalAlignment.Left;

        save.Click += (_, _) =>
        {
            var name = nameBox.Text?.Trim() ?? "";
            var system = systemBox.Text?.Trim() ?? "";
            var executable = pathBox.Text?.Trim() ?? "";

            if (name.Length == 0 ||
                system.Length == 0 ||
                executable.Length == 0)
            {
                SetStatus(
                    "Enter a name, system, and executable path.");
                return;
            }

            try
            {
                _database.SaveEmulator(
                    new EmulatorProfile
                    {
                        Id = emulator?.Id ??
                            Guid.NewGuid().ToString("N"),
                        Name = name,
                        System = system,
                        ExecutablePath = executable,
                        ArgumentsTemplate =
                            argsBox.Text?.Trim() ?? "\"{rom}\""
                    },
                    _activeProfile?.Id);

                RefreshData();
                _editingEmulatorProfileId = null;

                SetStatus("Saved emulator profile: " + name);
                Navigate("Emulators");
            }
            catch (UnauthorizedAccessException ex)
            {
                SetStatus(ex.Message);
            }
        };

        var cancel = MakeButton(
            "Cancel", CardBrush, MainTextBrush);

        cancel.HorizontalAlignment = HorizontalAlignment.Left;

        cancel.Click += (_, _) =>
        {
            _editingEmulatorProfileId = null;
            Navigate("Emulators");
        };

        page.Children.Add(save);
        page.Children.Add(cancel);

        foreach (var field in new Control[]
        {
            nameBox, systemBox, pathBox, browse,
            argsBox, save, cancel
        })
        {
            if (field is Button button)
                _controllerFocus.Register(button, () =>
                    button.RaiseEvent(
                        new Avalonia.Interactivity.RoutedEventArgs(
                            Button.ClickEvent)));
            else if (field is TextBox textBox)
                _controllerFocus.Register(textBox, () =>
                    textBox.Focus());
        }

        return new ScrollViewer
        {
            Content = page,
            VerticalScrollBarVisibility =
                ScrollBarVisibility.Auto
        };
    }


    // PROFILE GAME SAVES DASHBOARD
    private Control BuildSavesPage()
    {
        var page = new StackPanel
        {
            Spacing = 18,
            Margin = new Thickness(12)
        };

        page.Children.Add(
            Text("GAME SAVES", 26, MainTextBrush, true));

        var profileName =
            _activeProfile?.Name ?? "No profile selected";

        page.Children.Add(
            Text("Active Profile: " + profileName,
                16, AccentBrush, true));

        page.Children.Add(
            Text(
                "Detected emulator save locations. " +
                "Existing files are not modified.",
                13, MutedBrush));

        var detector =
            new XSphere.Services.SaveDetectionService();

        var locations = detector.Detect();

        if (locations.Count == 0)
        {
            page.Children.Add(
                Text("No save directories detected.",
                    14, MutedBrush));
        }

        foreach (var location in locations)
        {
            var info = new StackPanel
            {
                Spacing = 8
            };

            info.Children.Add(
                Text(location.Emulator + " — " +
                    location.Category,
                    18, MainTextBrush, true));

            info.Children.Add(
                Text("Files detected: " +
                    location.FileCount,
                    14, AccentBrush));

            info.Children.Add(
                Text(location.Directory,
                    12, MutedBrush,
                    wrapping: TextWrapping.Wrap));

            page.Children.Add(new Border
            {
                Background = PanelBrush,
                BorderBrush = GlassBorderBrush(),
            BoxShadow = ThemeGlow(),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(16),
                Padding = new Thickness(18),
                Child = info
            });
        }

        return new ScrollViewer
        {
            Content = page,
            VerticalScrollBarVisibility =
                ScrollBarVisibility.Auto
        };
    }


    private Control BuildSettingsPage()
    {
        var page = new StackPanel
        {
            Spacing = 20,
            Margin = new Thickness(12)
        };

        page.Children.Add(
            Text("SETTINGS", 26, MainTextBrush, true));

        page.Children.Add(
            Text("Manage your XSphere experience.",
                13, MutedBrush));

        var profileSection = new StackPanel
        {
            Spacing = 12
        };

        profileSection.Children.Add(
            Text("PROFILE", 18, MainTextBrush, true));

        profileSection.Children.Add(
            Text("Active Profile: " +
                (_activeProfile?.Name ?? "None"),
                14, AccentBrush));

        var switchButton = new Button
        {
            Content = "Switch Profile",
            Background = Brush(ThemeColor("#245C3B", "#244A7B", "#713044", "#59377C")),
            Foreground = MainTextBrush,
            Padding = new Thickness(18, 12),
            HorizontalAlignment = HorizontalAlignment.Left,
            CornerRadius = new CornerRadius(10)
        };

        switchButton.Click += (_, _) =>
            ShowProfileSelection();

        profileSection.Children.Add(switchButton);

        page.Children.Add(new Border
        {
            Background = PanelBrush,
            BorderBrush = GlassBorderBrush(),
            BoxShadow = ThemeGlow(),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(16),
            Padding = new Thickness(20),
            Child = profileSection
        });



        // PROFILE SECURITY SETTINGS
        if (_activeProfile is not null)
        {
            var activeProfile = _database.GetUserProfiles()
                .Find(p => p.Id == _activeProfile.Id);

            if (activeProfile is not null)
            {
                var securitySection = new StackPanel
                {
                    Spacing = 12
                };

                securitySection.Children.Add(
                    Text("PROFILE SECURITY", 18, MainTextBrush, true));

                securitySection.Children.Add(
                    Text(
                        activeProfile.HasPin
                            ? "PIN protection is enabled."
                            : "PIN protection is disabled.",
                        13, MutedBrush));

                if (activeProfile.HasPin)
                    securitySection.Children.Add(
                        Text("Current PIN", 13, MainTextBrush));

                var currentPinBox = NewTextBox("Current PIN");
                currentPinBox.PasswordChar = '●';
                currentPinBox.MaxLength = 8;
                currentPinBox.IsVisible = activeProfile.HasPin;
                securitySection.Children.Add(currentPinBox);

                securitySection.Children.Add(
                    Text("New PIN", 13, MainTextBrush));

                var newPinBox = NewTextBox("New PIN (4–8 digits)");
                newPinBox.PasswordChar = '●';
                newPinBox.MaxLength = 8;
                securitySection.Children.Add(newPinBox);

                var confirmBox = NewTextBox("Confirm new PIN");
                confirmBox.PasswordChar = '●';
                confirmBox.MaxLength = 8;
                securitySection.Children.Add(confirmBox);

                var feedback = Text("", 13, MutedBrush);

                var savePinButton = MakeButton(
                    activeProfile.HasPin ? "Change PIN" : "Set PIN",
                    AccentBrush,
                    BackgroundBrush);

                savePinButton.HorizontalAlignment =
                    HorizontalAlignment.Left;

                savePinButton.Click += (_, _) =>
                {
                    var currentPin = currentPinBox.Text ?? "";
                    var newPin = newPinBox.Text ?? "";
                    var confirmation = confirmBox.Text ?? "";

                    if (newPin != confirmation)
                    {
                        feedback.Text = "New PINs do not match.";
                        return;
                    }

                    if (!ProfileSecurityService.IsValidPin(newPin))
                    {
                        feedback.Text = "PIN must contain 4 to 8 digits.";
                        return;
                    }

                    try
                    {
                        if (!_database.ChangeProfilePin(
                            activeProfile.Id,
                            currentPin,
                            newPin))
                        {
                            feedback.Text =
                                _database.IsProfilePinLocked(activeProfile.Id)
                                    ? "Profile temporarily locked. " +
                                      "Please wait five minutes and try again."
                                    : "Current PIN is incorrect.";
                            return;
                        }

                        _activeProfile.HasPin = true;
                        RenderCurrentPage();
                    }
                    catch (Exception ex)
                    {
                        feedback.Text = "Could not update PIN: " + ex.Message;
                    }
                };

                securitySection.Children.Add(savePinButton);

                if (activeProfile.HasPin && !activeProfile.IsAdmin)
                {
                    var removePinButton = MakeButton(
                        "Remove PIN",
                        CardBrush,
                        MainTextBrush);

                    removePinButton.HorizontalAlignment =
                        HorizontalAlignment.Left;

                    removePinButton.Click += (_, _) =>
                    {
                        try
                        {
                            if (!_database.ChangeProfilePin(
                                activeProfile.Id,
                                currentPinBox.Text ?? "",
                                null))
                            {
                                feedback.Text =
                                    _database.IsProfilePinLocked(activeProfile.Id)
                                        ? "Profile temporarily locked. " +
                                          "Please wait five minutes and try again."
                                        : "Current PIN is incorrect.";
                                return;
                            }

                            _activeProfile.HasPin = false;
                            RenderCurrentPage();
                        }
                        catch (Exception ex)
                        {
                            feedback.Text =
                                "Could not remove PIN: " + ex.Message;
                        }
                    };

                    securitySection.Children.Add(removePinButton);
                }

                securitySection.Children.Add(feedback);

                page.Children.Add(new Border
                {
                    Background = PanelBrush,
                    BorderBrush = GlassBorderBrush(),
                    BoxShadow = ThemeGlow(),
                    BorderThickness = new Thickness(1),
                    CornerRadius = new CornerRadius(16),
                    Padding = new Thickness(20),
                    Child = securitySection
                });
            }
        }

        // FIRST ADMINISTRATOR SETUP
        var adminProfiles = _database.GetUserProfiles();

        if (!adminProfiles.Any(p => p.IsAdmin))
        {
            var adminSetupSection = new StackPanel
            {
                Spacing = 12
            };

            adminSetupSection.Children.Add(
                Text("ADMINISTRATOR SETUP",
                    18, MainTextBrush, true));

            adminSetupSection.Children.Add(
                Text(
                    "XSphere has no administrator yet. " +
                    "Choose an existing PIN-protected profile " +
                    "to become the first administrator.",
                    13, MutedBrush,
                    wrapping: TextWrapping.Wrap));

            var eligibleProfiles = adminProfiles
                .Where(p => p.HasPin)
                .ToList();

            if (eligibleProfiles.Count == 0)
            {
                adminSetupSection.Children.Add(
                    Text(
                        "Set a PIN on a profile before " +
                        "creating the first administrator.",
                        13, MutedBrush));
            }
            else
            {
                var profileSelector = new ComboBox
                {
                    Width = 240,
                    HorizontalAlignment = HorizontalAlignment.Left,
                    ItemsSource = eligibleProfiles
                        .Select(p => p.Name)
                        .ToList(),
                    SelectedIndex = 0
                };

                adminSetupSection.Children.Add(profileSelector);

                var adminPinBox = NewTextBox(
                    "Selected profile's PIN");
                adminPinBox.PasswordChar = '●';
                adminPinBox.MaxLength = 8;

                adminSetupSection.Children.Add(adminPinBox);

                var adminFeedback = Text(
                    "", 13, MutedBrush);

                var createAdminButton = MakeButton(
                    "Create First Administrator",
                    AccentBrush,
                    BackgroundBrush);

                createAdminButton.HorizontalAlignment =
                    HorizontalAlignment.Left;

                createAdminButton.Click += (_, _) =>
                {
                    var index = profileSelector.SelectedIndex;

                    if (index < 0 ||
                        index >= eligibleProfiles.Count)
                    {
                        adminFeedback.Text =
                            "Select a profile.";
                        return;
                    }

                    var selectedProfile =
                        eligibleProfiles[index];

                    var pin = adminPinBox.Text ?? "";

                    if (!ProfileSecurityService.IsValidPin(pin))
                    {
                        adminFeedback.Text =
                            "Enter the selected profile's PIN.";
                        return;
                    }

                    try
                    {
                        if (!_database.BootstrapFirstAdministrator(
                            selectedProfile.Id, pin))
                        {
                            adminFeedback.Text =
                                "PIN incorrect or administrator " +
                                "setup already completed.";
                            return;
                        }

                        if (_activeProfile?.Id ==
                            selectedProfile.Id)
                        {
                            _activeProfile.IsAdmin = true;
                        }

                        RenderCurrentPage();
                    }
                    catch (Exception ex)
                    {
                        adminFeedback.Text =
                            "Administrator setup failed: " +
                            ex.Message;
                    }
                };

                adminSetupSection.Children.Add(
                    createAdminButton);

                adminSetupSection.Children.Add(
                    adminFeedback);
            }

            page.Children.Add(new Border
            {
                Background = PanelBrush,
                BorderBrush = GlassBorderBrush(),
                BoxShadow = ThemeGlow(),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(16),
                Padding = new Thickness(20),
                Child = adminSetupSection
            });
        }

        // ADMINISTRATOR MANAGEMENT PANEL
        if (_activeProfile is not null)
        {
            var managementProfiles = _database.GetUserProfiles();
            var actingAdministrator = managementProfiles
                .FirstOrDefault(p =>
                    p.Id == _activeProfile.Id && p.IsAdmin);

            if (actingAdministrator is not null)
            {
                var adminSection = new StackPanel
                {
                    Spacing = 12
                };

                adminSection.Children.Add(
                    Text("ADMINISTRATOR MANAGEMENT",
                        18, MainTextBrush, true));

                adminSection.Children.Add(
                    Text(
                        "Promote or demote profiles. " +
                        "Every change requires your administrator PIN.",
                        13, MutedBrush,
                        wrapping: TextWrapping.Wrap));

                var otherProfiles = managementProfiles
                    .Where(p => p.Id != actingAdministrator.Id)
                    .ToList();

                if (otherProfiles.Count == 0)
                {
                    adminSection.Children.Add(
                        Text("No other profiles available.",
                            13, MutedBrush));
                }
                else
                {
                    var targetSelector = new ComboBox
                    {
                        Width = 260,
                        HorizontalAlignment =
                            HorizontalAlignment.Left,
                        ItemsSource = otherProfiles
                            .Select(p => p.Name +
                                (p.IsAdmin ? " (Administrator)" :
                                    " (Standard)"))
                            .ToList(),
                        SelectedIndex = 0
                    };

                    adminSection.Children.Add(
                        Text("Select profile", 13, MainTextBrush));

                    adminSection.Children.Add(targetSelector);

                    var adminPinBox = NewTextBox(
                        "Your administrator PIN");
                    adminPinBox.PasswordChar = '●';
                    adminPinBox.MaxLength = 8;

                    adminSection.Children.Add(
                        Text("Administrator verification",
                            13, MainTextBrush));

                    adminSection.Children.Add(adminPinBox);

                    var feedback = Text("", 13, MutedBrush);
                    adminSection.Children.Add(feedback);

                    var actionButton = MakeButton(
                        "Update Administrator Status",
                        AccentBrush,
                        BackgroundBrush);

                    actionButton.HorizontalAlignment =
                        HorizontalAlignment.Left;

                    void RefreshActionLabel()
                    {
                        int index = targetSelector.SelectedIndex;

                        if (index < 0 ||
                            index >= otherProfiles.Count)
                            return;

                        var target = otherProfiles[index];

                        actionButton.Content = target.IsAdmin
                            ? "Demote Administrator"
                            : "Promote to Administrator";
                    }

                    targetSelector.SelectionChanged += (_, _) =>
                    {
                        adminPinBox.Text = "";
                        feedback.Text = "";
                        RefreshActionLabel();
                    };

                    RefreshActionLabel();

                    actionButton.Click += (_, _) =>
                    {
                        int index = targetSelector.SelectedIndex;

                        if (index < 0 ||
                            index >= otherProfiles.Count)
                        {
                            feedback.Text = "Select a profile.";
                            return;
                        }

                        var target = otherProfiles[index];
                        bool makeAdmin = !target.IsAdmin;

                        if (makeAdmin && !target.HasPin)
                        {
                            feedback.Text =
                                "The selected profile must set " +
                                "a PIN before becoming an administrator.";
                            return;
                        }

                        string pin = adminPinBox.Text ?? "";

                        if (!ProfileSecurityService.IsValidPin(pin))
                        {
                            feedback.Text =
                                "Enter your administrator PIN.";
                            return;
                        }

                        try
                        {
                            bool updated =
                                _database.ChangeAdministratorStatus(
                                    actingAdministrator.Id,
                                    pin,
                                    target.Id,
                                    makeAdmin);

                            adminPinBox.Text = "";

                            if (!updated)
                            {
                                feedback.Text =
                                    _database.IsProfilePinLocked(
                                        actingAdministrator.Id)
                                        ? "Administrator PIN temporarily locked. " +
                                          "Please wait five minutes and try again."
                                        : "Change denied. Check your PIN " +
                                          "and administrator permissions.";
                                return;
                            }

                            RenderCurrentPage();
                        }
                        catch (Exception ex)
                        {
                            adminPinBox.Text = "";
                            feedback.Text =
                                "Could not update administrator: " +
                                ex.Message;
                        }
                    };

                    adminSection.Children.Add(actionButton);
                }

                page.Children.Add(new Border
                {
                    Background = PanelBrush,
                    BorderBrush = GlassBorderBrush(),
                    BoxShadow = ThemeGlow(),
                    BorderThickness = new Thickness(1),
                    CornerRadius = new CornerRadius(16),
                    Padding = new Thickness(20),
                    Child = adminSection
                });
            }
        }


        // PROFILE PERMISSIONS PANEL
        if (_activeProfile is not null)
        {
            var allProfiles = _database.GetUserProfiles();

            var actingAdmin = allProfiles.FirstOrDefault(p =>
                p.Id == _activeProfile.Id && p.IsAdmin);

            if (actingAdmin is not null)
            {
                var standardProfiles = allProfiles
                    .Where(p => !p.IsAdmin)
                    .ToList();

                var permissionsSection = new StackPanel
                {
                    Spacing = 12
                };

                permissionsSection.Children.Add(
                    Text("PROFILE PERMISSIONS",
                        18, MainTextBrush, true));

                permissionsSection.Children.Add(
                    Text(
                        "Configure standard profile access. " +
                        "Changes require your administrator PIN.",
                        13, MutedBrush,
                        wrapping: TextWrapping.Wrap));

                if (standardProfiles.Count == 0)
                {
                    permissionsSection.Children.Add(
                        Text("No standard profiles available.",
                            13, MutedBrush));
                }
                else
                {
                    var profileSelector = new ComboBox
                    {
                        Width = 260,
                        HorizontalAlignment =
                            HorizontalAlignment.Left,
                        ItemsSource = standardProfiles
                            .Select(p => p.Name)
                            .ToList(),
                        SelectedIndex = 0
                    };

                    permissionsSection.Children.Add(
                        Text("Select standard profile",
                            13, MainTextBrush));

                    permissionsSection.Children.Add(profileSelector);

                    var gamesCheck = new CheckBox
                    {
                        Content = "Allow launching games"
                    };

                    var emulatorsCheck = new CheckBox
                    {
                        Content = "Allow managing emulators"
                    };

                    var settingsCheck = new CheckBox
                    {
                        Content = "Allow modifying settings"
                    };

                    permissionsSection.Children.Add(gamesCheck);
                    permissionsSection.Children.Add(emulatorsCheck);
                    permissionsSection.Children.Add(settingsCheck);

                    permissionsSection.Children.Add(
                        Text(
                            "Profile management remains " +
                            "administrator-only.",
                            12, MutedBrush,
                            wrapping: TextWrapping.Wrap));

                    var pinBox = NewTextBox(
                        "Your administrator PIN");

                    pinBox.PasswordChar = '●';
                    pinBox.MaxLength = 8;

                    permissionsSection.Children.Add(
                        Text("Administrator verification",
                            13, MainTextBrush));

                    permissionsSection.Children.Add(pinBox);

                    var feedback = Text("", 13, MutedBrush);
                    permissionsSection.Children.Add(feedback);

                    void LoadPermissions()
                    {
                        int index = profileSelector.SelectedIndex;

                        if (index < 0 ||
                            index >= standardProfiles.Count)
                            return;

                        var target = standardProfiles[index];
                        var permissions =
                            _database.GetProfilePermissions(target.Id);

                        gamesCheck.IsChecked =
                            permissions.CanLaunchGames;

                        emulatorsCheck.IsChecked =
                            permissions.CanManageEmulators;

                        settingsCheck.IsChecked =
                            permissions.CanModifySettings;

                        pinBox.Text = "";
                        feedback.Text = "";
                    }

                    profileSelector.SelectionChanged += (_, _) =>
                        LoadPermissions();

                    LoadPermissions();

                    var saveButton = MakeButton(
                        "Save Permissions",
                        AccentBrush,
                        BackgroundBrush);

                    saveButton.HorizontalAlignment =
                        HorizontalAlignment.Left;

                    saveButton.Click += (_, _) =>
                    {
                        int index = profileSelector.SelectedIndex;

                        if (index < 0 ||
                            index >= standardProfiles.Count)
                        {
                            feedback.Text = "Select a profile.";
                            return;
                        }

                        string pin = pinBox.Text ?? "";

                        if (!ProfileSecurityService.IsValidPin(pin))
                        {
                            feedback.Text =
                                "Enter your administrator PIN.";
                            return;
                        }

                        var target = standardProfiles[index];

                        var updatedPermissions =
                            new ProfilePermissions
                            {
                                ProfileId = target.Id,
                                CanLaunchGames =
                                    gamesCheck.IsChecked == true,
                                CanManageEmulators =
                                    emulatorsCheck.IsChecked == true,
                                CanModifySettings =
                                    settingsCheck.IsChecked == true,
                                CanManageProfiles = false
                            };

                        try
                        {
                            bool updated =
                                _database.ChangeProfilePermissions(
                                    actingAdmin.Id,
                                    pin,
                                    updatedPermissions);

                            pinBox.Text = "";

                            if (!updated)
                            {
                                feedback.Text =
                                    _database.IsProfilePinLocked(
                                        actingAdmin.Id)
                                        ? "Administrator PIN temporarily locked. " +
                                          "Please wait five minutes and try again."
                                        : "Permission update denied. " +
                                          "Check your administrator PIN.";
                                return;
                            }

                            feedback.Text =
                                "Profile permissions saved successfully.";
                        }
                        catch (Exception ex)
                        {
                            pinBox.Text = "";
                            feedback.Text =
                                "Could not save permissions: " +
                                ex.Message;
                        }
                    };

                    permissionsSection.Children.Add(saveButton);
                }

                page.Children.Add(new Border
                {
                    Background = PanelBrush,
                    BorderBrush = GlassBorderBrush(),
                    BoxShadow = ThemeGlow(),
                    BorderThickness = new Thickness(1),
                    CornerRadius = new CornerRadius(16),
                    Padding = new Thickness(20),
                    Child = permissionsSection
                });
            }
        }

        // FEATURED GAME APPEARANCE SETTINGS
        var appearanceSection = new StackPanel
        {
            Spacing = 14
        };

        appearanceSection.Children.Add(
            Text("APPEARANCE", 18, MainTextBrush, true));
        appearanceSection.Children.Add(
            Text("Color theme", 14, MutedBrush));

        var themeNames = new[]
        {
            "Emerald Glass",
            "Midnight Blue",
            "Crimson Red",
            "Royal Purple"
        };

        var themeSelector = new ComboBox
        {
            Width = 220,
            HorizontalAlignment = HorizontalAlignment.Left,
            ItemsSource = themeNames,
            SelectedItem = _theme
        };

        themeSelector.SelectionChanged += (_, _) =>
        {
            if (themeSelector.SelectedItem is not string selected)
                return;

            if (_theme == selected)
                return;

            _theme = selected;
            _appSettings.Theme = selected;
            _appSettingsService.Save(_appSettings);

            Background = BackgroundBrush;
            RebuildShell();
        };

        appearanceSection.Children.Add(themeSelector);

        appearanceSection.Children.Add(
            Text("Background style", 14, MutedBrush));

        var backgroundStyles = new[]
        {
            "Solid Color",
            "Frosted Glass",
            "Hexagonal Tech",
            "Ambient Waves",
            "Retro Arcade"
        };

        var backgroundStyleSelector = new ComboBox
        {
            Width = 220,
            HorizontalAlignment = HorizontalAlignment.Left,
            ItemsSource = backgroundStyles,
            SelectedItem = Array.Exists(
                backgroundStyles,
                x => x == _appSettings.BackgroundStyle)
                ? _appSettings.BackgroundStyle
                : "Solid Color"
        };

        backgroundStyleSelector.SelectionChanged += (_, _) =>
        {
            if (backgroundStyleSelector.SelectedItem
                is not string selected)
                return;

            if (_appSettings.BackgroundStyle == selected)
                return;

            _appSettings.BackgroundStyle = selected;
            _appSettingsService.Save(_appSettings);
            RebuildShell();
        };

        appearanceSection.Children.Add(backgroundStyleSelector);

        appearanceSection.Children.Add(
            Text("Glass transparency", 14, MutedBrush));

        var glassLabel = Text(
            Math.Round(_appSettings.GlassTransparency) + "%",
            13, AccentBrush);

        appearanceSection.Children.Add(glassLabel);

        var glassSlider = new Slider
        {
            Minimum = 0,
            Maximum = 80,
            Value = Math.Clamp(
                _appSettings.GlassTransparency, 0, 80),
            Width = 240,
            TickFrequency = 5,
            IsSnapToTickEnabled = true,
            HorizontalAlignment = HorizontalAlignment.Left
        };

        glassSlider.ValueChanged += (_, _) =>
        {
            _appSettings.GlassTransparency = glassSlider.Value;
            _glassTransparency = glassSlider.Value;

            glassLabel.Text =
                Math.Round(glassSlider.Value) + "%";

            _appSettingsService.Save(_appSettings);
        };

        appearanceSection.Children.Add(glassSlider);

        appearanceSection.Children.Add(
            Text("Glow intensity", 14, MutedBrush));

        var glowLabel = Text(
            Math.Round(_appSettings.GlowIntensity) + "%",
            13, AccentBrush);

        appearanceSection.Children.Add(glowLabel);

        var glowSlider = new Slider
        {
            Minimum = 0,
            Maximum = 100,
            Value = Math.Clamp(
                _appSettings.GlowIntensity, 0, 100),
            Width = 240,
            TickFrequency = 5,
            IsSnapToTickEnabled = true,
            HorizontalAlignment = HorizontalAlignment.Left
        };

        glowSlider.ValueChanged += (_, _) =>
        {
            _appSettings.GlowIntensity = glowSlider.Value;
            _glowIntensity = glowSlider.Value;

            glowLabel.Text =
                Math.Round(glowSlider.Value) + "%";

            _appSettingsService.Save(_appSettings);
        };

        appearanceSection.Children.Add(glowSlider);

        appearanceSection.Children.Add(
            Text("HOME DASHBOARD", 18, MainTextBrush, true));

        appearanceSection.Children.Add(
            Text("Dashboard glass transparency", 14, MutedBrush));

        var homeGlassLabel = Text(
            Math.Round(_appSettings.HomeGlassTransparency) + "%",
            13, AccentBrush);

        appearanceSection.Children.Add(homeGlassLabel);

        var homeGlassSlider = new Slider
        {
            Minimum = 0,
            Maximum = 80,
            Value = Math.Clamp(
                _appSettings.HomeGlassTransparency, 0, 80),
            Width = 240,
            TickFrequency = 5,
            IsSnapToTickEnabled = true,
            HorizontalAlignment = HorizontalAlignment.Left
        };

        homeGlassSlider.ValueChanged += (_, _) =>
        {
            _appSettings.HomeGlassTransparency =
                homeGlassSlider.Value;

            _homeGlassTransparency = homeGlassSlider.Value;

            homeGlassLabel.Text =
                Math.Round(homeGlassSlider.Value) + "%";

            _appSettingsService.Save(_appSettings);
        };

        appearanceSection.Children.Add(homeGlassSlider);

        appearanceSection.Children.Add(
            Text("Dashboard glow intensity", 14, MutedBrush));

        var homeGlowLabel = Text(
            Math.Round(_appSettings.HomeGlowIntensity) + "%",
            13, AccentBrush);

        appearanceSection.Children.Add(homeGlowLabel);

        var homeGlowSlider = new Slider
        {
            Minimum = 0,
            Maximum = 100,
            Value = Math.Clamp(
                _appSettings.HomeGlowIntensity, 0, 100),
            Width = 240,
            TickFrequency = 5,
            IsSnapToTickEnabled = true,
            HorizontalAlignment = HorizontalAlignment.Left
        };

        homeGlowSlider.ValueChanged += (_, _) =>
        {
            _appSettings.HomeGlowIntensity =
                homeGlowSlider.Value;

            _homeGlowIntensity = homeGlowSlider.Value;

            homeGlowLabel.Text =
                Math.Round(homeGlowSlider.Value) + "%";

            _appSettingsService.Save(_appSettings);
        };

        appearanceSection.Children.Add(homeGlowSlider);

        appearanceSection.Children.Add(
            Text("Dashboard changes apply when you open Home.",
                12, MutedBrush));


        appearanceSection.Children.Add(
            Text("Glass and glow changes apply when you reopen a page.",
                12, MutedBrush));


        var intensityLabel = Text(
            "Texture intensity: " +
            Math.Round(_appSettings.BackgroundIntensity) + "%",
            14, MutedBrush);

        appearanceSection.Children.Add(intensityLabel);

        var intensitySlider = new Slider
        {
            Minimum = 0,
            Maximum = 100,
            Value = Math.Clamp(
                _appSettings.BackgroundIntensity, 0, 100),
            Width = 240,
            HorizontalAlignment = HorizontalAlignment.Left,
            TickFrequency = 5,
            IsSnapToTickEnabled = true
        };

        intensitySlider.ValueChanged += (_, _) =>
        {
            _appSettings.BackgroundIntensity =
                intensitySlider.Value;

            intensityLabel.Text =
                "Texture intensity: " +
                Math.Round(intensitySlider.Value) + "%";

            _appSettingsService.Save(_appSettings);

            // Update the existing background without
            // rebuilding the Settings page during dragging.
            var texture = FindBackgroundTexture();
            if (texture is not null)
            {
                texture.Intensity = intensitySlider.Value;
                texture.InvalidateVisual();
            }
        };

        appearanceSection.Children.Add(intensitySlider);



        var autoRotateToggle = new CheckBox
        {
            Content = "Automatically rotate Featured Games",
            IsChecked = _appSettings.AutoRotateFeatured,
            Foreground = MainTextBrush
        };

        autoRotateToggle.IsCheckedChanged += (_, _) =>
        {
            _appSettings.AutoRotateFeatured =
                autoRotateToggle.IsChecked == true;

            _appSettingsService.Save(_appSettings);
        };

        appearanceSection.Children.Add(autoRotateToggle);

        appearanceSection.Children.Add(
            Text("Featured Game rotation interval",
                14, MutedBrush));

        var intervalBox = new ComboBox
        {
            Width = 180,
            HorizontalAlignment = HorizontalAlignment.Left,
            ItemsSource = new[]
            {
                "5 seconds",
                "8 seconds",
                "10 seconds",
                "15 seconds"
            }
        };

        var intervals = new[] { 5, 8, 10, 15 };

        var selectedInterval =
            Array.IndexOf(intervals,
                _appSettings.FeaturedRotationSeconds);

        intervalBox.SelectedIndex =
            selectedInterval >= 0 ? selectedInterval : 1;

        intervalBox.SelectionChanged += (_, _) =>
        {
            if (intervalBox.SelectedIndex < 0)
                return;

            _appSettings.FeaturedRotationSeconds =
                intervals[intervalBox.SelectedIndex];

            _appSettingsService.Save(_appSettings);
        };

        appearanceSection.Children.Add(intervalBox);

        appearanceSection.Children.Add(
            Text("Changes apply the next time Home is opened.",
                12, MutedBrush));

        page.Children.Add(new Border
        {
            Background = PanelBrush,
            BorderBrush = GlassBorderBrush(),
            BoxShadow = ThemeGlow(),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(16),
            Padding = new Thickness(20),
            Child = appearanceSection
        });

        var launchSection = new StackPanel
        {
            Spacing = 14
        };

        launchSection.Children.Add(
            Text("GAME LAUNCHING", 18, MainTextBrush, true));

        var minimizeToggle = new CheckBox
        {
            Content = "Minimize XSphere when launching a game",
            IsChecked = _appSettings.MinimizeOnLaunch,
            Foreground = MainTextBrush
        };

        minimizeToggle.IsCheckedChanged += (_, _) =>
        {
            _appSettings.MinimizeOnLaunch =
                minimizeToggle.IsChecked == true;

            _appSettingsService.Save(_appSettings);
        };

        launchSection.Children.Add(minimizeToggle);

        var restoreToggle = new CheckBox
        {
            Content = "Restore XSphere after a game closes",
            IsChecked = _appSettings.RestoreAfterExit,
            Foreground = MainTextBrush
        };

        restoreToggle.IsCheckedChanged += (_, _) =>
        {
            _appSettings.RestoreAfterExit =
                restoreToggle.IsChecked == true;

            _appSettingsService.Save(_appSettings);
        };

        launchSection.Children.Add(restoreToggle);

        page.Children.Add(new Border
        {
            Background = PanelBrush,
            BorderBrush = GlassBorderBrush(),
            BoxShadow = ThemeGlow(),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(16),
            Padding = new Thickness(20),
            Child = launchSection
        });

        var applicationSection = new StackPanel
        {
            Spacing = 10
        };

        applicationSection.Children.Add(
            Text("APPLICATION", 18, MainTextBrush, true));

        applicationSection.Children.Add(
            Text("XSphere", 15, AccentBrush, true));

        applicationSection.Children.Add(
            Text("Version 0.1", 13, MutedBrush));

        applicationSection.Children.Add(
            Text("Emerald Glass Interface",
                13, MutedBrush));

        page.Children.Add(new Border
        {
            Background = PanelBrush,
            BorderBrush = GlassBorderBrush(),
            BoxShadow = ThemeGlow(),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(16),
            Padding = new Thickness(20),
            Child = applicationSection
        });

        return new ScrollViewer
        {
            Content = page,
            VerticalScrollBarVisibility =
                ScrollBarVisibility.Auto
        };
    }

    private Control BuildFoldersPage()
    {
        var page = new StackPanel { Spacing = 15 };
        page.Children.Add(Text("Choose the directories that contain your legally obtained game files. Scanning never moves or modifies your ROMs.", 13, MutedBrush, wrapping: TextWrapping.Wrap));
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 9 };
        var add = MakeButton("+ Add ROM Folder", AccentBrush, BackgroundBrush);
        add.Click += async (_, _) => await AddRomFolderAsync();
        var scan = MakeButton("Scan All Folders", CardBrush, MainTextBrush);
        scan.Click += async (_, _) => await ScanAllFoldersAsync();
        buttons.Children.Add(add);
        buttons.Children.Add(scan);
        page.Children.Add(buttons);
        page.Children.Add(Text($"Saved folders ({_romFolders.Count})", 17, MainTextBrush, true));

        if (_romFolders.Count == 0)
            page.Children.Add(Text("No folders added yet. Start by adding the folder where you keep your ROM files.", 12, MutedBrush));

        foreach (var folder in _romFolders)
        {
            var row = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), Background = PanelBrush, Margin = new Thickness(0, 0, 0, 8) };
            var pathText = Text(folder, 12, MainTextBrush, wrapping: TextWrapping.Wrap, margin: new Thickness(12, 12));
            row.Children.Add(pathText);
            var remove = MakeButton("Remove", CardBrush, MutedBrush);
            remove.Margin = new Thickness(8);
            remove.VerticalAlignment = VerticalAlignment.Center;
            remove.Click += (_, _) =>
            {
                _database.DeleteRomFolder(folder);
                RefreshData();
                RenderCurrentPage();
            };
            Grid.SetColumn(remove, 1);
            row.Children.Add(remove);
            page.Children.Add(row);
        }

        page.Children.Add(Text("Supported scan formats include NES, SNES, Game Boy, GBA, N64, DS, 3DS, and common disc/container formats such as ISO, CHD, RVZ, WBFS, ZIP, and 7Z. Disc formats may need the system name in their folder path for automatic system matching.", 11, MutedBrush, wrapping: TextWrapping.Wrap));
        return new ScrollViewer { Content = page, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
    }

    private async Task AddRomFolderAsync()
    {
        try
        {
            var folders = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
            {
                Title = "Choose a folder containing your ROM files",
                AllowMultiple = false
            });
            if (folders.Count == 0) return;
            var path = folders[0].Path.LocalPath;
            _database.AddRomFolder(path);
            RefreshData();
            SetStatus("Added ROM folder. Starting scan...");
            await ScanAllFoldersAsync();
        }
        catch (Exception ex)
        {
            SetStatus("Could not add folder: " + ex.Message);
        }
    }

    private async Task ScanAllFoldersAsync()
    {
        var folders = _database.GetRomFolders();
        if (folders.Count == 0)
        {
            SetStatus("Add a ROM folder before scanning.");
            return;
        }

        SetStatus("Scanning ROM folders...");
        try
        {
            var discovered = await Task.Run(() =>
            {
                var list = new List<Game>();
                foreach (var folder in folders)
                {
                    if (Directory.Exists(folder)) list.AddRange(_scanner.ScanFolder(folder));
                }
                return list;
            });

            foreach (var game in discovered)
                _database.SaveGame(game);

            var removed = _database.RemoveInvalidGameEntries();
            RefreshData();

SetStatus(
    $"Scan complete. Found {discovered.Count} file(s); " +
    $"removed {removed} invalid entries; " +
    $"{_games.Count} game(s) are in your library.");

// Remember whether the controller was navigating the page.
bool restorePageFocus = _controllerPageFocused;

RenderCurrentPage();

if (restorePageFocus)
{
    Avalonia.Threading.Dispatcher.UIThread.Post(
        () =>
        {
            if (_currentPage is "Library" or "Favorites")
            {
                _controllerPageFocused = true;
                _controllerFocus.SelectFirst();
            }
        },
        Avalonia.Threading.DispatcherPriority.Loaded);
}
        }
        catch (Exception ex)
        {
            SetStatus("Scan failed: " + ex.Message);
        }
    }

    private async void LaunchGame(Game game)
{
    try
    {
        // Enforce profile permissions at the launch boundary.
        if (_activeProfile is null)
        {
            SetStatus("Game launch denied: No active profile.");
            return;
        }

        var permissions =
            _database.GetProfilePermissions(_activeProfile.Id);

        if (!permissions.CanLaunchGames)
        {
            SetStatus(
                "Game launch denied: Your profile does not " +
                "have permission to launch games.");
            return;
        }

        // Start the emulator and get its process.
        using var emulatorProcess =
            _launcher.Launch(game, _emulators);

        SetStatus("Playing " + game.Title);

        // Minimize XSphere while playing.
        if (_appSettings.MinimizeOnLaunch)
            WindowState = WindowState.Minimized;

        // Wait without freezing XSphere's interface.
Console.WriteLine(
    $"[XSphere] Waiting for process {emulatorProcess.Id}");

Console.WriteLine(
    $"[XSphere] HasExited before waiting: {emulatorProcess.HasExited}");
        await emulatorProcess.WaitForExitAsync();
Console.WriteLine(
    "[XSphere] Emulator process exited. Restoring window...");

        // Restore XSphere on Avalonia's UI thread.
await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
{
    Console.WriteLine("[XSphere] Restoring main window...");

    // Reset the minimized window before showing it again.
if (_appSettings.RestoreAfterExit)
{
    Hide();
    WindowState = WindowState.Normal;
    Show();
    Activate();
}

    SetStatus("Finished playing " + game.Title);

    Console.WriteLine(
        $"[XSphere] Window state: {WindowState}");
});
    }
    catch (Exception ex)
    {
        // Restore XSphere if an error occurs.
        WindowState = WindowState.Normal;

        SetStatus("Could not launch or monitor game: " + ex.Message);
    }
}
    private void OpenWebsite(string url)
    {
        try
        {
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            SetStatus("Could not open website: " + ex.Message);
        }
    }

    private void SetStatus(string message)
    {
        _statusMessage = message;
        if (_statusText is not null) _statusText.Text = message;
    }

    private static TextBox NewTextBox(string watermark) => new()
    {
        PlaceholderText = watermark,
        Height = 40,
        VerticalContentAlignment = VerticalAlignment.Center
    };

    private static TextBlock Text(
        string value,
        double size,
        IBrush color,
        bool bold = false,
        TextWrapping wrapping = TextWrapping.NoWrap,
        TextTrimming? trimming = null,
        Thickness? margin = null) => new()
    {
        Text = value,
        FontSize = size,
        Foreground = color,
        FontWeight = bold ? FontWeight.Bold : FontWeight.Normal,
        VerticalAlignment = VerticalAlignment.Center,
        TextWrapping = wrapping,
        TextTrimming = trimming ?? TextTrimming.None,
        Margin = margin ?? new Thickness(0)
    };

    // THEME-AWARE BUTTON STYLE
    private static Button MakeButton(
        string label,
        IBrush background,
        IBrush foreground,
        bool titanium = false)
    {
        var normalBorder = titanium
            ? Brush("#A7B4C2")
            : Brush(ThemeColor(
                "#557463",
                "#354A69",
                "#59313E",
                "#49345F"));

        var hoverBorder = titanium
            ? Brush("#B8D9F2")
            : AccentBrush;

        var button = new Button
        {
            Content = label,
            Background = background,
            Foreground = foreground,
            Padding = new Thickness(15, 10),
            MinHeight = 40,
            FontSize = 13,
            CornerRadius = new CornerRadius(16),
            BorderBrush = normalBorder,
            BorderThickness = new Thickness(1),
            HorizontalContentAlignment =
                HorizontalAlignment.Center,
            VerticalContentAlignment =
                VerticalAlignment.Center
        };

        button.PointerEntered += (_, _) =>
        {
            button.BorderBrush = hoverBorder;
        };

        button.PointerExited += (_, _) =>
        {
            button.BorderBrush = normalBorder;
        };

        return button;
    }


    // EMERALD GLASS MATERIALS
    private static IBrush GlassBrush(byte opacity = 205)
    {
        return new SolidColorBrush(
            Color.FromArgb(opacity, 25, 45, 33));
    }

    private static IBrush GlassBorderBrush()
    {
        // Match the selected XSphere theme.
        var borderColor = Color.Parse(ThemeColor(
            "#5FA071",  // Emerald Glass
            "#608DC4",  // Midnight Blue
            "#C46B80",  // Crimson Red
            "#A17BCB"   // Royal Purple
        ));

        return new SolidColorBrush(
            Color.FromArgb(
                170,
                borderColor.R,
                borderColor.G,
                borderColor.B));
    }

    private static IBrush SystemColor(string system)
    {
        var colors = new[] { "#354C7A", "#68456F", "#315F5B", "#74533A", "#3C5670", "#66523C" };
        var index = (int)(unchecked((uint)system.GetHashCode()) % (uint)colors.Length);
        return Brush(colors[index]);
    }

    private static string SystemGlyph(string system) => system switch
    {
        "NES" or "Super Nintendo" or "Nintendo 64" => "🎮",
        "Game Boy" or "Game Boy Color" or "Game Boy Advance" or "Nintendo DS" => "🕹",
        "PlayStation" or "PlayStation 2" or "PlayStation 3" or "PlayStation Portable" => "▣",
        "GameCube" or "Wii" or "Nintendo Switch" => "◆",
        _ => "🎮"
    };
}
