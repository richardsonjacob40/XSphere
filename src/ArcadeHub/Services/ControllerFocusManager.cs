using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace ArcadeHub.Services;

public sealed class ControllerFocusManager
{
    private sealed record FocusItem(Control Control, Action Activate);

    private readonly List<FocusItem> _items = new();
    private int _selectedIndex = -1;
    private Control? _highlightedControl;
    private IBrush? _originalBrush;
    private Thickness _originalThickness;

    public int Count => _items.Count;

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

        if (control is ContentControl content &&
            content.Content is Control child)
            Visit(child);

        if (control is Panel panel)
            foreach (var panelChild in panel.Children)
                Visit(panelChild);

        if (control is Decorator decorator &&
            decorator.Child is Control decoratedChild)
            Visit(decoratedChild);

        if (control is ItemsControl items)
            foreach (var item in items.Items)
                if (item is Control itemControl)
                    Visit(itemControl);
    }

    public void ClearSelection()
    {
        RemoveHighlight();
        _selectedIndex = -1;
    }

    public void SelectFirst()
    {
        if (_items.Count > 0)
            Select(0);
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

            if (forward <= 5)
                continue;

            // Favor controls in the requested direction,
            // with a penalty for moving across rows/columns.
            double score =
                forward +
                Math.Abs(sideways) * 2.5 +
                (sideways * sideways) / (forward + 1);

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

        _highlightedControl = null;
    }
}
