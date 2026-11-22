using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;

namespace XSphere.Controls;

public sealed class VirtualPinKeypad : Border
{
    private readonly TextBox _target;
    private readonly Action? _onEnter;
    private readonly Button?[,] _buttons = new Button?[5, 3];
    private Button? _visibilityButton;
    private bool _visibilitySelected;
    private int _selectedRow;
    private int _selectedColumn;

    public event Action? CloseRequested;

    public VirtualPinKeypad(TextBox target, Action? onEnter = null)
    {
        _target = target;
        _onEnter = onEnter;

        Background = new SolidColorBrush(Color.Parse("#17211D"));
        BorderBrush = new SolidColorBrush(Color.Parse("#43E68D"));
        BorderThickness = new Thickness(2);
        CornerRadius = new CornerRadius(18);
        Padding = new Thickness(20);
        Width = 320;

        var layout = new StackPanel { Spacing = 12 };

        layout.Children.Add(new TextBlock
        {
            Text = "ENTER PIN",
            FontSize = 20,
            FontWeight = FontWeight.Bold,
            Foreground = Brushes.White,
            HorizontalAlignment = HorizontalAlignment.Center
        });

        var pinDisplay = new TextBox
        {
            Text = _target.Text ?? "",
            PasswordChar = '●',
            IsReadOnly = true,
            IsTabStop = false,
            FontSize = 24,
            Height = 52,
            HorizontalContentAlignment = HorizontalAlignment.Center,
            VerticalContentAlignment = VerticalAlignment.Center,
            Background = new SolidColorBrush(Color.Parse("#25322B")),
            Foreground = Brushes.White
        };

        _target.TextChanged += (_, _) =>
        {
            pinDisplay.Text = _target.Text ?? "";
        };

        var visibilityButton = new Button
        {
            Content = "Show PIN",
            Height = 42,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Background = new SolidColorBrush(Color.Parse("#25322B")),
            Foreground = Brushes.White
        };

        _visibilityButton = visibilityButton;

        bool pinVisible = false;

        visibilityButton.Click += (_, _) =>
        {
            pinVisible = !pinVisible;
            pinDisplay.PasswordChar = pinVisible ? '\0' : '●';
            visibilityButton.Content = pinVisible ? "Hide PIN" : "Show PIN";
        };

        layout.Children.Add(pinDisplay);
        layout.Children.Add(visibilityButton);

        var keys = new Grid
        {
            RowDefinitions = new RowDefinitions("Auto,Auto,Auto,Auto,Auto"),
            ColumnDefinitions = new ColumnDefinitions("*,*,*"),
            RowSpacing = 8,
            ColumnSpacing = 8
        };

        string[,] labels =
        {
            { "1", "2", "3" },
            { "4", "5", "6" },
            { "7", "8", "9" },
            { "Clear", "0", "⌫" },
            { "Cancel", "Enter", "" }
        };

        for (int row = 0; row < 5; row++)
        {
            for (int col = 0; col < 3; col++)
            {
                string label = labels[row, col];

                if (label.Length == 0)
                    continue;

                var button = new Button
                {
                    Content = label,
                    Height = 54,
                    MinWidth = 70,
                    FontSize = 18,
                    HorizontalContentAlignment = HorizontalAlignment.Center,
                    HorizontalAlignment = HorizontalAlignment.Stretch,
                    Background = new SolidColorBrush(Color.Parse("#25322B")),
                    Foreground = Brushes.White
                };

                button.Click += (_, _) => PressKey(label);
                _buttons[row, col] = button;

                Grid.SetRow(button, row);
                Grid.SetColumn(button, col);
                keys.Children.Add(button);
            }
        }

        layout.Children.Add(keys);

        layout.Children.Add(new TextBlock
        {
            Text = "D-pad: Navigate    A: Select    B: Close\n" +
                   "X: Backspace    Y: Enter",
            FontSize = 13,
            Foreground = Brushes.White,
            TextAlignment = TextAlignment.Center,
            TextWrapping = TextWrapping.Wrap
        });

        Child = layout;
        UpdateSelection();
    }

    public void HandleControllerInput(string input)
    {
        if (input == "X")
        {
            PressKey("⌫");
            return;
        }

        if (input == "Y")
        {
            PressKey("Enter");
            return;
        }

        switch (input)
        {
            case "B":
                CloseRequested?.Invoke();
                return;
            case "A":
                if (_visibilitySelected)
                {
                    _visibilityButton?.RaiseEvent(
                        new Avalonia.Interactivity.RoutedEventArgs(
                            Button.ClickEvent));
                }
                else if (_buttons[_selectedRow, _selectedColumn]?.Content is string key)
                {
                    PressKey(key);
                }
                return;
        }

        int dx = input switch
        {
            "DpadLeft" or "XNegative" => -1,
            "DpadRight" or "XPositive" => 1,
            _ => 0
        };

        int dy = input switch
        {
            "DpadUp" or "YNegative" => -1,
            "DpadDown" or "YPositive" => 1,
            _ => 0
        };

        if (dx == 0 && dy == 0)
            return;

        if (_visibilitySelected)
        {
            if (dy > 0)
            {
                _visibilitySelected = false;
                _selectedRow = 0;
                UpdateSelection();
            }
            return;
        }

        if (dy < 0 && _selectedRow == 0)
        {
            _visibilitySelected = true;
            UpdateSelection();
            return;
        }

        int row = _selectedRow + dy;
        int col = _selectedColumn + dx;

        if (row < 0 || row >= 5 || col < 0 || col >= 3)
            return;

        if (_buttons[row, col] is null)
            return;

        _selectedRow = row;
        _selectedColumn = col;
        UpdateSelection();
    }

    private void UpdateSelection()
    {
        if (_visibilityButton is not null)
        {
            _visibilityButton.BorderBrush = _visibilitySelected
                ? Brushes.LimeGreen
                : Brushes.Transparent;

            _visibilityButton.BorderThickness = _visibilitySelected
                ? new Thickness(3)
                : new Thickness(0);
        }

        for (int row = 0; row < 5; row++)
        {
            for (int col = 0; col < 3; col++)
            {
                var button = _buttons[row, col];
                if (button is null)
                    continue;

                bool selected = !_visibilitySelected &&
                                row == _selectedRow &&
                                col == _selectedColumn;

                button.BorderBrush = selected
                    ? Brushes.LimeGreen
                    : Brushes.Transparent;

                button.BorderThickness = selected
                    ? new Thickness(3)
                    : new Thickness(0);
            }
        }
    }

    public void PressKey(string key)
    {
        string current = _target.Text ?? "";

        switch (key)
        {
            case "Clear":
                _target.Text = "";
                break;

            case "⌫":
                if (current.Length > 0)
                    _target.Text = current[..^1];
                break;

            case "Cancel":
                CloseRequested?.Invoke();
                break;

            case "Enter":
                _onEnter?.Invoke();
                CloseRequested?.Invoke();
                break;

            default:
                if (key.Length == 1 &&
                    char.IsDigit(key[0]) &&
                    current.Length < (_target.MaxLength > 0
                        ? _target.MaxLength
                        : 8))
                {
                    _target.Text = current + key;
                }
                break;
        }
    }
}
