using System;
using System.Windows;
using System.Windows.Media;
using System.Windows.Shapes;

namespace BabyShop;

public class HeartParticle
{
    private static readonly Random Random = new();

    private static readonly Color[] Colors =
    [
        Color.FromArgb(128, 255, 255, 255),
        Color.FromArgb(90, 255, 255, 255),
        Color.FromArgb(77, 239, 111, 154),
        Color.FromArgb(115, 255, 200, 225)
    ];

    private readonly double _canvasWidth;
    private readonly double _canvasHeight;

    private double _x;
    private double _y;
    private double _speed;
    private double _wobble;
    private double _wobbleSpeed;
    private double _drift;
    private double _angle;
    private double _spin;
    private double _alpha;
    private double _scale;
    private double _size;

    public HeartParticle(double canvasWidth, double canvasHeight, bool randomY = false)
    {
        _canvasWidth = canvasWidth;
        _canvasHeight = canvasHeight;

        Shape = BuildHeartPath();
        Reset(randomY);
    }

    public Path Shape { get; }

    private static Path BuildHeartPath()
    {
        var geometry = Geometry.Parse("M 0,-3.5 C 5,-8 10,-2 0,4 C -10,-2 -5,-8 0,-3.5 Z");

        return new Path
        {
            Data = geometry,
            Stretch = Stretch.None,
            IsHitTestVisible = false
        };
    }

    private void Reset(bool randomY = false)
    {
        _x = Random.NextDouble() * _canvasWidth;
        _y = randomY ? Random.NextDouble() * _canvasHeight : _canvasHeight + 20;
        _speed = 0.22 + (Random.NextDouble() * 0.34);
        _wobble = Random.NextDouble() * Math.PI * 2;
        _wobbleSpeed = 0.018 + (Random.NextDouble() * 0.018);
        _drift = (Random.NextDouble() - 0.5) * 0.22;
        _angle = (Random.NextDouble() - 0.5) * 0.4;
        _spin = (Random.NextDouble() - 0.5) * 0.012;
        _alpha = 0.20 + (Random.NextDouble() * 0.32);
        _scale = 0.48 + (Random.NextDouble() * 0.26);
        _size = 4 + (Random.NextDouble() * 7);

        var color = Colors[Random.Next(Colors.Length)];
        Shape.Fill = new SolidColorBrush(Color.FromArgb((byte)(_alpha * 255), color.R, color.G, color.B));
        Shape.Stroke = Brushes.Transparent;
        Shape.RenderTransformOrigin = new Point(0.5, 0.5);

        ApplyTransform();
    }

    private void ApplyTransform()
    {
        var group = new TransformGroup();
        group.Children.Add(new ScaleTransform(_scale * _size, _scale * _size));
        group.Children.Add(new RotateTransform(_angle * (180 / Math.PI)));
        group.Children.Add(new TranslateTransform(_x, _y));
        Shape.RenderTransform = group;
    }

    public void Update()
    {
        _y -= _speed;
        _wobble += _wobbleSpeed;
        _x += Math.Sin(_wobble) * 0.45 + _drift;
        _angle += _spin;

        if (_y < -30)
        {
            Reset();
            return;
        }

        if (Shape.RenderTransform is not TransformGroup transformGroup || transformGroup.Children.Count != 3)
        {
            return;
        }

        if (transformGroup.Children[1] is RotateTransform rotateTransform)
        {
            rotateTransform.Angle = _angle * (180.0 / Math.PI);
        }

        if (transformGroup.Children[2] is TranslateTransform translateTransform)
        {
            translateTransform.X = _x;
            translateTransform.Y = _y;
        }
    }
}
