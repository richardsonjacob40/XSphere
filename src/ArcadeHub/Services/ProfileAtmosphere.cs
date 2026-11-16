using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace ArcadeHub.Services;

public sealed class ProfileAtmosphere : Control
{
    public string AccentColor { get; set; } = "#B8D9F2";

    private readonly DispatcherTimer _timer;
    private readonly DateTime _started = DateTime.UtcNow;

    public ProfileAtmosphere()
    {
        IsHitTestVisible = false;

        _timer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(33)
        };

        _timer.Tick += (_, _) => InvalidateVisual();

        AttachedToVisualTree += (_, _) => _timer.Start();
        DetachedFromVisualTree += (_, _) => _timer.Stop();
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);

        double width = Bounds.Width;
        double height = Bounds.Height;

        if (width <= 0 || height <= 0)
            return;

        var accent = Color.Parse(AccentColor);
        var random = new Random(84721);

        double elapsed =
            (DateTime.UtcNow - _started).TotalSeconds;

        // Small particles drifting upward.
        for (int i = 0; i < 145; i++)
        {
            double x = random.NextDouble() * width;
            double initialY = random.NextDouble() * height;
            double radius = 0.7 + random.NextDouble() * 2.6;

            double speed = 3 + random.NextDouble() * 12;

            double y = (
                initialY - elapsed * speed % height
                + height
            ) % height;

            byte alpha = (byte)(25 + random.Next(80));

            var particle = new SolidColorBrush(
                Color.FromArgb(
                    alpha,
                    accent.R,
                    accent.G,
                    accent.B));

            context.DrawEllipse(
                particle,
                null,
                new Point(x, y),
                radius,
                radius);
        }

        // Larger atmospheric particles with gentle movement.
        for (int i = 0; i < 18; i++)
        {
            double x = random.NextDouble() * width;
            double initialY = random.NextDouble() * height;
            double radius = 5 + random.NextDouble() * 17;

            double speed = 1 + random.NextDouble() * 4;

            double y = (
                initialY - elapsed * speed % height
                + height
            ) % height;

            var haze = new SolidColorBrush(
                Color.FromArgb(
                    8,
                    accent.R,
                    accent.G,
                    accent.B));

            context.DrawEllipse(
                haze,
                null,
                new Point(x, y),
                radius,
                radius);
        }
    }
}
