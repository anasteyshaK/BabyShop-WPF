using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace BabyShop;

public class HeartsOverlay : Canvas
{
    private const int HeartCount = 9;

    private readonly List<HeartParticle> _particles = [];
    private bool _running;

    public HeartsOverlay()
    {
        ClipToBounds = true;
        IsHitTestVisible = false;
        Background = Brushes.Transparent;

        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
        SizeChanged += OnSizeChanged;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        InitializeParticles();
        _running = true;
        CompositionTarget.Rendering += OnRendering;
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        _running = false;
        CompositionTarget.Rendering -= OnRendering;
    }

    private void OnSizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (!_running)
        {
            return;
        }

        InitializeParticles();
    }

    private void InitializeParticles()
    {
        _particles.Clear();
        Children.Clear();

        var width = ActualWidth > 0 ? ActualWidth : 528;
        var height = ActualHeight > 0 ? ActualHeight : 670;

        for (var i = 0; i < HeartCount; i++)
        {
            var particle = new HeartParticle(width, height, randomY: true);
            _particles.Add(particle);
            Children.Add(particle.Shape);
        }
    }

    private void OnRendering(object? sender, EventArgs e)
    {
        if (!_running)
        {
            return;
        }

        foreach (var particle in _particles)
        {
            particle.Update();
        }
    }
}
