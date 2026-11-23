using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace XSphere.Services;

public sealed class ControllerFocusManager
{
    private sealed record FocusItem(Control Control, Action Activate);

    private readonly List<FocusItem> _items = new();
    private int _selectedIndex = -1;
    private Control? _highlightedControl;
    private IBrush? _originalBrush;
    private Thickness _originalThickness;

    public int Count => _items.Count;

    public Slider? SelectedSlider =>
        _selectedIndex >= 0 &&
        _selectedIndex < _items.Count
            ? _items[_selectedIndex].Control as Slider
            : null;

    public Control? SelectedControl =>
        _selectedIndex >= 0 &&
        _selectedIndex < _items.Count
            ? _items[_selectedIndex].Control
            : null;

    public ComboBox? SelectedComboBox =>
        _selectedIndex >= 0 &&
        _selectedIndex < _items.Count
            ? _items[_selectedIndex].Control as ComboBox
            : null;

    public void Clear()
    {
        ClearSelection();
        _items.Clear();
    }

    public void Register(Control control, Action activate)
    {
        if (_items.Exists(item => ReferenceEquals(item.Control, control)))
            return;

        _items.Add(new FocusItem(control, activate));

        string description = control switch
        {
            CheckBox checkBox => checkBox.Content?.ToString() ?? "(checkbox)",
            Button button => button.Content?.ToString() ?? "(button)",
            TextBox textBox => textBox.PlaceholderText ?? "(text field)",
            ComboBox => "(dropdown)",
            Border => "(game card or custom control)",
            _ => "(other)"
        };

        Console.WriteLine(
            $"[Controller Register] {control.GetType().Name}: {description}");
    }

    public void Unregister(Control control)
    {
        int index = _items.FindIndex(
            item => ReferenceEquals(item.Control, control));

        if (index < 0)
            return;

        if (_selectedIndex == index)
        {
            ClearSelection();
        }
        else if (_selectedIndex > index)
        {
            _selectedIndex--;
        }

        _items.RemoveAt(index);
    }

    public void RegisterButtons(Control root)
    {
        Visit(root);
    }

    private void Visit(Control control)
    {
        if (control is Button button && button.IsEnabled)
        {
            Register(button, () =>
                button.RaiseEvent(
                    new Avalonia.Interactivity.RoutedEventArgs(
                        Button.ClickEvent)));
        }

        if (control is ComboBox comboBox && comboBox.IsEnabled)
        {
            Register(comboBox, () =>
                comboBox.IsDropDownOpen = !comboBox.IsDropDownOpen);
        }

        if (control is TextBox textBox && textBox.IsEnabled)
        {
            Register(textBox, () => textBox.Focus());
        }

        if (control is Slider slider && slider.IsEnabled)
        {
            Register(slider, () => slider.Focus());
        }

        if (control is CheckBox checkBox && checkBox.IsEnabled)
        {
            Register(checkBox, () =>
                checkBox.IsChecked = checkBox.IsChecked != true);
        }

        // Traverse the logical control hierarchy first.
        // This includes page content inside ScrollViewer.
        if (control is ContentControl content &&
            content.Content is Control contentChild)
        {
            Visit(contentChild);
        }

        if (control is Panel panel)
        {
            foreach (var child in panel.Children)
                Visit(child);
        }

        if (control is Decorator decorator &&
            decorator.Child is Control decoratedChild)
        {
            Visit(decoratedChild);
        }

        // Also discover controls created by Avalonia templates.
        foreach (var visualChild in
                 Avalonia.VisualTree.VisualExtensions.GetVisualChildren(control))
        {
            if (visualChild is Control child)
                Visit(child);
        }
    }

    public bool SelectControl(Control control)
    {
        int index = _items.FindIndex(item =>
            ReferenceEquals(item.Control, control));

        if (index < 0 ||
            !control.IsVisible ||
            !control.IsEnabled ||
            TopLevel.GetTopLevel(control) is null ||
            control.Bounds.Width <= 0 ||
            control.Bounds.Height <= 0)
            return false;

        Select(index);
        return true;
    }

    public void ClearSelection()
    {
        RemoveHighlight();
        _selectedIndex = -1;
    }

    public void SelectFirst()
    {
        Console.WriteLine(
            $"[Controller Focus] SelectFirst: Count={_items.Count}");

        for (int i = 0; i < _items.Count; i++)
        {
            var control = _items[i].Control;
            var root = TopLevel.GetTopLevel(control);

            Console.WriteLine(
                $"[Controller Focus] Index={i}, " +
                $"Type={control.GetType().Name}, " +
                $"Visible={control.IsVisible}, " +
                $"Enabled={control.IsEnabled}, " +
                $"Attached={root is not null}, " +
                $"Bounds={control.Bounds}");

            if (root is not null &&
                control.IsVisible &&
                control.IsEnabled &&
                control.Bounds.Width > 0 &&
                control.Bounds.Height > 0)
            {
                Select(i);
                return;
            }
        }

        Console.WriteLine("[Controller Focus] No usable controls found.");
    }

    // Navigate controls in registration order instead of screen position.
    // Useful for forms and vertically arranged action lists.
    public bool MoveSequential(int direction)
    {
        if (_items.Count == 0 || direction == 0)
            return false;

        if (_selectedIndex < 0)
        {
            SelectFirst();
            return _selectedIndex >= 0;
        }

        for (int i = _selectedIndex + Math.Sign(direction);
             i >= 0 && i < _items.Count;
             i += Math.Sign(direction))
        {
            var control = _items[i].Control;

            if (TopLevel.GetTopLevel(control) is null ||
                !control.IsVisible ||
                !control.IsEnabled ||
                control.Bounds.Width <= 0 ||
                control.Bounds.Height <= 0)
                continue;

            Select(i);
            return true;
        }

        return false;
    }

    // For emulator action rows, preserve the Edit/Remove column
    // when moving vertically. Other controls use registration order.
    public bool MoveEmulatorVertical(int direction)
    {
        if (_items.Count == 0 || direction == 0)
            return false;

        if (_selectedIndex < 0)
        {
            SelectFirst();
            return _selectedIndex >= 0;
        }

        var current = _items[_selectedIndex].Control;
        string? column = current is Button button
            ? button.Content?.ToString()
            : null;

        if (column != "Edit" && column != "Remove")
            return MoveSequential(direction);

        for (int i = _selectedIndex + Math.Sign(direction);
             i >= 0 && i < _items.Count;
             i += Math.Sign(direction))
        {
            var candidate = _items[i].Control;

            if (candidate is not Button candidateButton ||
                candidateButton.Content?.ToString() != column)
                continue;

            if (TopLevel.GetTopLevel(candidate) is null ||
                !candidate.IsVisible ||
                !candidate.IsEnabled ||
                candidate.Bounds.Width <= 0 ||
                candidate.Bounds.Height <= 0)
                continue;

            Select(i);
            return true;
        }

        // At the end of the emulator list, continue to the
        // downloads or Create button using normal navigation.
        return MoveSequential(direction);
    }

    // Navigate emulator action columns and horizontal download buttons.
    public bool MoveEmulatorNavigation(int dx, int dy)
    {
        if (_items.Count == 0)
            return false;

        if (_selectedIndex < 0)
        {
            SelectFirst();
            return _selectedIndex >= 0;
        }

        bool Usable(Control control) =>
            TopLevel.GetTopLevel(control) is not null &&
            control.IsVisible &&
            control.IsEnabled &&
            control.Bounds.Width > 0 &&
            control.Bounds.Height > 0;

        string Label(Control control) =>
            control is Button button
                ? button.Content?.ToString() ?? ""
                : "";

        bool IsDownload(Control control) =>
            Label(control).EndsWith(" ↗");

        bool SelectMatching(int start, int step,
            Func<Control, bool> matches)
        {
            for (int i = start;
                 i >= 0 && i < _items.Count;
                 i += step)
            {
                var candidate = _items[i].Control;

                if (matches(candidate) && Usable(candidate))
                {
                    Select(i);
                    return true;
                }
            }

            return false;
        }

        var current = _items[_selectedIndex].Control;
        var label = Label(current);

        if (IsDownload(current))
        {
            if (dy > 0)
                return false;

            if (dy < 0)
            {
                if (SelectMatching(_selectedIndex - 1, -1,
                    control => Label(control) == "Edit"))
                    return true;

                return SelectMatching(_selectedIndex - 1, -1,
                    control => Label(control) ==
                        "+ Create Emulator Profile");
            }

            if (dx != 0)
                return SelectMatching(
                    _selectedIndex + Math.Sign(dx),
                    Math.Sign(dx),
                    control => IsDownload(control));

            return false;
        }

        if (dy != 0)
        {
            if (label == "Edit" || label == "Remove")
            {
                if (SelectMatching(
                    _selectedIndex + Math.Sign(dy),
                    Math.Sign(dy),
                    control => Label(control) == label))
                    return true;

                if (dy > 0)
                    return SelectMatching(0, 1, IsDownload);

                return SelectMatching(_selectedIndex - 1, -1,
                    control => Label(control) ==
                        "+ Create Emulator Profile");
            }

            if (dy > 0 &&
                label == "+ Create Emulator Profile")
            {
                if (SelectMatching(0, 1,
                    control => Label(control) == "Edit"))
                    return true;

                return SelectMatching(0, 1, IsDownload);
            }

            return false;
        }

        if (dx != 0)
            return MoveDirectional(dx, 0);

        return false;
    }

    // dx/dy: -1, 0, or 1.
    // Returns false when there is no selectable control
    // in the requested direction.
    public bool MoveDirectional(int dx, int dy)
    {
        if (_items.Count == 0 || (dx == 0 && dy == 0))
            return false;

        if (_selectedIndex < 0)
        {
            SelectFirst();
            return true;
        }

        var current = _items[_selectedIndex].Control;
        var root = TopLevel.GetTopLevel(current);

        if (root is null)
            return false;

        var currentPoint = GetCenter(current, root);
        if (currentPoint is null)
            return false;

        int bestIndex = -1;
        double bestScore = double.MaxValue;

        for (int i = 0; i < _items.Count; i++)
        {
            if (i == _selectedIndex)
                continue;

            var candidate = _items[i].Control;

            if (!candidate.IsVisible || !candidate.IsEnabled)
                continue;

            var candidatePoint = GetCenter(candidate, root);
            if (candidatePoint is null)
                continue;

            double horizontal =
                candidatePoint.Value.X - currentPoint.Value.X;

            double vertical =
                candidatePoint.Value.Y - currentPoint.Value.Y;

            double forward = horizontal * dx + vertical * dy;
            double sideways = -horizontal * dy + vertical * dx;

            // Ignore controls that are not actually in the
            // requested direction.
            if (forward <= 5)
                continue;

            // Prefer controls whose horizontal or vertical
            // ranges overlap with the current control.
            var currentBounds = current.Bounds;
            var candidateBounds = candidate.Bounds;

            double currentHalfWidth = currentBounds.Width / 2;
            double currentHalfHeight = currentBounds.Height / 2;
            double candidateHalfWidth = candidateBounds.Width / 2;
            double candidateHalfHeight = candidateBounds.Height / 2;

            double perpendicularGap;

            if (dx != 0)
            {
                perpendicularGap = Math.Max(
                    0,
                    Math.Abs(vertical) -
                    currentHalfHeight -
                    candidateHalfHeight);
            }
            else
            {
                perpendicularGap = Math.Max(
                    0,
                    Math.Abs(horizontal) -
                    currentHalfWidth -
                    candidateHalfWidth);
            }

            // Favor controls in the requested direction,
            // with a penalty for moving across rows/columns.
            double score =
                forward +
                perpendicularGap * 3.0 +
                Math.Abs(sideways) * 0.1;

            if (score < bestScore)
            {
                bestScore = score;
                bestIndex = i;
            }
        }

        if (bestIndex < 0)
            return false;

        Select(bestIndex);
        return true;
    }

    private static Point? GetCenter(Control control, Visual root)
    {
        if (control.Bounds.Width <= 0 ||
            control.Bounds.Height <= 0)
            return null;

        return control.TranslatePoint(
            new Point(
                control.Bounds.Width / 2,
                control.Bounds.Height / 2),
            root);
    }

    public void Activate()
    {
        if (_selectedIndex >= 0 &&
            _selectedIndex < _items.Count)
            _items[_selectedIndex].Activate();
    }

    private void Select(int index)
    {
        RemoveHighlight();

        _selectedIndex = index;
        var control = _items[index].Control;

        if (control is Border border)
        {
            _highlightedControl = border;
            _originalBrush = border.BorderBrush;
            _originalThickness = border.BorderThickness;

            border.BorderBrush = Brushes.LimeGreen;
            border.BorderThickness = new Thickness(3);
        }
        else if (control is Button button)
        {
            _highlightedControl = button;
            _originalBrush = button.BorderBrush;
            _originalThickness = button.BorderThickness;

            button.BorderBrush = Brushes.LimeGreen;
            button.BorderThickness = new Thickness(3);
        }
        else if (control is ComboBox comboBox)
        {
            _highlightedControl = comboBox;
            _originalBrush = comboBox.BorderBrush;
            _originalThickness = comboBox.BorderThickness;

            comboBox.BorderBrush = Brushes.LimeGreen;
            comboBox.BorderThickness = new Thickness(3);
        }

        if (control is Slider slider)
        {
            _highlightedControl = slider;
            _originalBrush = slider.BorderBrush;
            _originalThickness = slider.BorderThickness;
            slider.BorderBrush = Brushes.LimeGreen;
            slider.BorderThickness = new Thickness(3);
        }
        else if (control is TextBox textBox)
        {
            _highlightedControl = textBox;
            _originalBrush = textBox.BorderBrush;
            _originalThickness = textBox.BorderThickness;

            textBox.BorderBrush = Brushes.LimeGreen;
            textBox.BorderThickness = new Thickness(3);
        }
        else if (control is CheckBox checkBox)
        {
            _highlightedControl = checkBox;
            _originalBrush = checkBox.BorderBrush;
            _originalThickness = checkBox.BorderThickness;

            checkBox.BorderBrush = Brushes.LimeGreen;
            checkBox.BorderThickness = new Thickness(3);
        }

        control.BringIntoView();
    }

    private void RemoveHighlight()
    {
        if (_highlightedControl is Border border)
        {
            border.BorderBrush = _originalBrush;
            border.BorderThickness = _originalThickness;
        }
        else if (_highlightedControl is Button button)
        {
            button.BorderBrush = _originalBrush;
            button.BorderThickness = _originalThickness;
        }
        else if (_highlightedControl is ComboBox comboBox)
        {
            comboBox.BorderBrush = _originalBrush;
            comboBox.BorderThickness = _originalThickness;
        }

        else if (_highlightedControl is Slider slider)
        {
            slider.BorderBrush = _originalBrush;
            slider.BorderThickness = _originalThickness;
        }
        else if (_highlightedControl is TextBox textBox)
        {
            textBox.BorderBrush = _originalBrush;
            textBox.BorderThickness = _originalThickness;
        }
        else if (_highlightedControl is CheckBox checkBox)
        {
            checkBox.BorderBrush = _originalBrush;
            checkBox.BorderThickness = _originalThickness;
        }

        _highlightedControl = null;
    }
}
