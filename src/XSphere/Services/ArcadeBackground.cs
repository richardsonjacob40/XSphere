using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace XSphere.Services;

public sealed class ArcadeBackground : Control
{
    public string Style { get; set; } = "Solid Color";
    public string AccentColor { get; set; } = "#43E68D";
    public double Intensity { get; set; } = 35;

    public override void Render(DrawingContext context)
    {
        base.Render(context);

        double w = Bounds.Width;
        double h = Bounds.Height;

        if (w <= 0 || h <= 0 || Style == "Solid Color")
            return;

        var accent = Color.Parse(AccentColor);
        double strength = Math.Clamp(Intensity, 0, 100) / 100.0;

        SolidColorBrush Ink(double opacity)
        {
            byte alpha = (byte)Math.Clamp(
                Math.Round(255 * opacity * strength),
                0, 255);

            return new SolidColorBrush(
                Color.FromArgb(
                    alpha, accent.R, accent.G, accent.B));
        }

        void Line(double x1, double y1, double x2, double y2,
                  double opacity = 0.3, double thickness = 1)
        {
            context.DrawLine(
                new Pen(Ink(opacity), thickness),
                new Point(x1, y1),
                new Point(x2, y2));
        }

        switch (Style)
        {
            case "Frosted Glass":
            {
                for (int i = 0; i < 5; i++)
                {
                    double x = w * (i * 0.25 - 0.15);
                    double y = h * (0.12 + (i % 3) * 0.28);

                    context.DrawGeometry(
                        Ink(0.09),
                        new Pen(Ink(0.25), 1.5),
                        Geometry.Parse(
                            $"M {x:F1},{y:F1} " +
                            $"L {x + 240:F1},{y - 70:F1} " +
                            $"L {x + 340:F1},{y + 120:F1} " +
                            $"L {x + 100:F1},{y + 190:F1} Z"));
                }
                break;
            }

            case "Hexagonal Tech":
            {
                const double radius = 27;
                double dx = Math.Sqrt(3) * radius;
                double dy = 1.5 * radius;

                for (int row = -1; row < h / dy + 2; row++)
                {
                    for (int col = -1; col < w / dx + 2; col++)
                    {
                        double cx = col * dx +
                            (row % 2 == 0 ? 0 : dx / 2);
                        double cy = row * dy;

                        for (int side = 0; side < 6; side++)
                        {
                            double a = Math.PI / 3 * side - Math.PI / 6;
                            double b = Math.PI / 3 * (side + 1) - Math.PI / 6;

                            Line(
                                cx + radius * Math.Cos(a),
                                cy + radius * Math.Sin(a),
                                cx + radius * Math.Cos(b),
                                cy + radius * Math.Sin(b),
                                0.23);
                        }
                    }
                }
                break;
            }

            case "Ambient Waves":
            {
                for (int i = 0; i < 7; i++)
                {
                    double y = h * (0.15 + i * 0.12);

                    var wave = Geometry.Parse(
                        $"M {-100:F1},{y:F1} " +
                        $"C {w * 0.25:F1},{y - 140:F1} " +
                        $"{w * 0.55:F1},{y + 140:F1} " +
                        $"{w + 100:F1},{y - 40:F1}");

                    context.DrawGeometry(
                        null,
                        new Pen(Ink(0.45 - i * 0.035), 3),
                        wave);
                }
                break;
            }

            case "Retro Arcade":
            {
                double horizon = h * 0.47;
                double center = w / 2;

                for (int i = -12; i <= 12; i++)
                {
                    Line(
                        center + i * 12, horizon,
                        center + i * 140, h,
                        0.45);
                }

                for (int i = 1; i <= 12; i++)
                {
                    double t = i / 12.0;
                    double y = horizon +
                        (h - horizon) * t * t;

                    Line(0, y, w, y, 0.45);
                }

                Line(0, horizon, w, horizon, 0.6, 2);
                break;
            }
        }
    }
}
