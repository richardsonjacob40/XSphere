using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;

namespace XSphere.Controls;

public sealed class VirtualQwertyKeyboard : Border
{
    private readonly TextBox _target;
    private readonly Button?[,] _buttons = new Button?[5, 10];
    private int _selectedRow;
    private int _selectedColumn;
    private bool _shift;

    public event Action? CloseRequested;

    public VirtualQwertyKeyboard(TextBox target)
    {
        _target = target;

        Background = new SolidColorBrush(Color.Parse("#17211D"));
        BorderBrush = new SolidColorBrush(Color.Parse("#43E68D"));
        BorderThickness = new Thickness(2);
        CornerRadius = new CornerRadius(18);
        Padding = new Thickness(20);
        MaxWidth = 850;
        HorizontalAlignment = HorizontalAlignment.Center;
        VerticalAlignment = VerticalAlignment.Center;

        var layout = new StackPanel { Spacing = 12 };

        layout.Children.Add(new TextBlock
        {
            Text = "SEARCH GAMES",
            FontSize = 20,
            FontWeight = FontWeight.Bold,
            Foreground = Brushes.White,
            HorizontalAlignment = HorizontalAlignment.Center
        });

        var display = new TextBox
        {
            Text = _target.Text ?? "",
            IsReadOnly = true,
            IsTabStop = false,
            FontSize = 26,
            Height = 60,
            MinWidth = 550,
            VerticalContentAlignment = VerticalAlignment.Center,
            Background = new SolidColorBrush(Color.Parse("#25322B")),
            Foreground = Brushes.White
        };

        _target.TextChanged += (_, _) =>
        {
            display.Text = _target.Text ?? "";
        };

        layout.Children.Add(display);

        var keys = new Grid
        {
            RowDefinitions = new RowDefinitions(
                "Auto,Auto,Auto,Auto,Auto"),
            ColumnDefinitions = new ColumnDefinitions(
                "*,*,*,*,*,*,*,*,*,*"),
            RowSpacing = 7,
            ColumnSpacing = 7
        };

        string[][] rows =
        {
            new[] { "1", "2", "3", "4", "5", "6", "7", "8", "9", "0" },
            new[] { "q", "w", "e", "r", "t", "y", "u", "i", "o", "p" },
            new[] { "a", "s", "d", "f", "g", "h", "j", "k", "l" },
            new[] { "Shift", "z", "x", "c", "v", "b", "n", "m", "⌫" },
            new[] { "Clear", "Space", "Close" }
        };

        for (int row = 0; row < rows.Length; row++)
        {
            for (int col = 0; col < rows[row].Length; col++)
            {
                string label = rows[row][col];

                var button = new Button
                {
                    Content = label,
                    MinWidth = label.Length > 1 ? 75 : 54,
                    Height = 48,
                    FontSize = 17,
                    HorizontalContentAlignment = HorizontalAlignment.Center,
                    Background = new SolidColorBrush(Color.Parse("#25322B")),
                    Foreground = Brushes.White
                };

                button.Click += (_, _) => PressKey(label);

                _buttons[row, col] = button;
                Grid.SetRow(button, row);
                Grid.SetColumn(button, col);

                if (row == 4 && col == 1)
                    Grid.SetColumnSpan(button, 4);
                else if (row == 4 && col == 2)
                    Grid.SetColumn(button, 5);

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

    public void PressKey(string key)
    {
        string current = _target.Text ?? "";

        switch (key)
        {
            case "Close":
                CloseRequested?.Invoke();
                return;

            case "Clear":
                _target.Text = "";
                return;

            case "Space":
                _target.Text = current + " ";
                return;

            case "⌫":
                if (current.Length > 0)
                    _target.Text = current[..^1];
                return;

            case "Shift":
                _shift = !_shift;
                return;

            default:
                _target.Text = current +
                    (_shift ? key.ToUpperInvariant() : key);
                return;
        }
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
            CloseRequested?.Invoke();
            return;
        }

        if (input == "B")
        {
            CloseRequested?.Invoke();
            return;
        }

        if (input == "A")
        {
            if (_buttons[_selectedRow, _selectedColumn]?.Content is string key)
                PressKey(key);
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

        int row = _selectedRow + dy;
        int col = _selectedColumn + dx;

        if (row < 0 || row >= 5 || col < 0 || col >= 10)
            return;

        if (dy != 0)
        {
            if (_buttons[row, col] is null)
            {
                int nearest = -1;
                int distance = int.MaxValue;

                for (int i = 0; i < 10; i++)
                {
                    if (_buttons[row, i] is null)
                        continue;

                    int candidateDistance = Math.Abs(i - col);

                    if (candidateDistance < distance)
                    {
                        nearest = i;
                        distance = candidateDistance;
                    }
                }

                if (nearest < 0)
                    return;

                col = nearest;
            }
        }
        else
        {
            while (col >= 0 && col < 10 &&
                   _buttons[row, col] is null)
            {
                col += dx;
            }

            if (col < 0 || col >= 10)
                return;
        }

        _selectedRow = row;
        _selectedColumn = col;
        UpdateSelection();
    }

    private void UpdateSelection()
    {
        for (int row = 0; row < 5; row++)
        {
            for (int col = 0; col < 10; col++)
            {
                var button = _buttons[row, col];

                if (button is null)
                    continue;

                bool selected = row == _selectedRow &&
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
}
