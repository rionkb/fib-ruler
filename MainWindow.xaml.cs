using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using FibRuler.Models;
using FibRuler.Services;

namespace FibRuler;

public partial class MainWindow : Window
{
    private double _pointAX = 400;
    private double _pointAY = 700;
    private double _pointBX = 650;
    private double _pointBY = 300;
    private readonly IReadOnlyList<FibLevel> _levels = FibLevel.CreateDefaults();
    private readonly HotkeyService _hotkeyService = new();
    private RulerMode _mode = RulerMode.Edit;
    private DragTarget _dragTarget;

    public MainWindow()
    {
        InitializeComponent();
        SourceInitialized += MainWindow_SourceInitialized;
        Closed += MainWindow_Closed;
        _hotkeyService.HotkeyPressed += HotkeyService_HotkeyPressed;
    }

    private void MainWindow_SourceInitialized(object? sender, EventArgs e)
    {
        if (!_hotkeyService.Register(this))
        {
            System.Diagnostics.Debug.WriteLine("Fib Ruler: Ctrl+Alt+Fは使用できません。別のアプリが登録済みの可能性があります。");
        }

        ApplyMode(RulerMode.Edit);
    }

    private void MainWindow_Closed(object? sender, EventArgs e)
    {
        _hotkeyService.Dispose();
    }

    private void HotkeyService_HotkeyPressed(object? sender, EventArgs e)
    {
        ApplyMode(_mode == RulerMode.Edit ? RulerMode.Locked : RulerMode.Edit);
    }

    private void ApplyMode(RulerMode mode)
    {
        _mode = mode;
        if (_mode == RulerMode.Locked)
        {
            _dragTarget = DragTarget.None;
            if (OverlayCanvas.IsMouseCaptured)
            {
                OverlayCanvas.ReleaseMouseCapture();
            }
        }

        try
        {
            WindowInteropService.SetClickThrough(this, _mode == RulerMode.Locked);
        }
        catch (Exception exception)
        {
            System.Diagnostics.Debug.WriteLine($"Fib Ruler: モード切替に失敗しました。{exception}");
        }
    }

    private void Window_Loaded(object sender, RoutedEventArgs e) => UpdateRulerVisuals();

    private void Window_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (IsLoaded) UpdateRulerVisuals();
    }

    private void OverlayCanvas_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (_mode != RulerMode.Edit)
        {
            return;
        }

        if (e.OriginalSource is not Ellipse handle || handle.Tag is not DragTarget target)
        {
            _dragTarget = DragTarget.None;
            return;
        }

        _dragTarget = target;
        OverlayCanvas.CaptureMouse();
        e.Handled = true;
    }

    private void OverlayCanvas_MouseMove(object sender, MouseEventArgs e)
    {
        if (_mode != RulerMode.Edit || _dragTarget == DragTarget.None || e.LeftButton != MouseButtonState.Pressed)
        {
            return;
        }

        Point position = e.GetPosition(OverlayCanvas);
        if (_dragTarget == DragTarget.A)
        {
            _pointAX = position.X;
            _pointAY = position.Y;
        }
        else if (_dragTarget == DragTarget.B)
        {
            _pointBX = position.X;
            _pointBY = position.Y;
        }

        UpdateRulerVisuals();
        e.Handled = true;
    }

    private void OverlayCanvas_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (_dragTarget == DragTarget.None)
        {
            return;
        }

        _dragTarget = DragTarget.None;
        if (OverlayCanvas.IsMouseCaptured)
        {
            OverlayCanvas.ReleaseMouseCapture();
        }

        e.Handled = true;
    }

    private void UpdateRulerVisuals()
    {
        OverlayCanvas.Children.Clear();
        AddLine(_pointAX, _pointAY, _pointBX, _pointBY, Brushes.White, 2);
        AddPoint(_pointAX, _pointAY, Brushes.DodgerBlue, DragTarget.A);
        AddPoint(_pointBX, _pointBY, Brushes.OrangeRed, DragTarget.B);
        AddLabel("A", _pointAX + 8, _pointAY + 8, Brushes.DodgerBlue);
        AddLabel("B", _pointBX + 8, _pointBY - 26, Brushes.OrangeRed);

        foreach (FibLevel level in _levels.Where(static item => item.Enabled))
        {
            double y = FibCalculator.CalculateY(_pointAY, _pointBY, level.Ratio);
            AddLine(0, y, ActualWidth, y, Brushes.LimeGreen, 1.25);
            AddLabel(level.Label, Math.Max(8, ActualWidth - 78), y - 18, Brushes.LimeGreen);
        }
    }

    private void AddLine(double x1, double y1, double x2, double y2, Brush brush, double thickness)
    {
        OverlayCanvas.Children.Add(new Line
        {
            X1 = x1,
            Y1 = y1,
            X2 = x2,
            Y2 = y2,
            Stroke = brush,
            StrokeThickness = thickness,
            SnapsToDevicePixels = true
        });
    }

    private void AddPoint(double x, double y, Brush brush, DragTarget target)
    {
        const double diameter = 18;
        Ellipse point = new()
        {
            Width = diameter,
            Height = diameter,
            Fill = brush,
            Stroke = Brushes.White,
            StrokeThickness = 1,
            Tag = target,
            Cursor = Cursors.SizeAll
        };
        Canvas.SetLeft(point, x - diameter / 2);
        Canvas.SetTop(point, y - diameter / 2);
        OverlayCanvas.Children.Add(point);
    }

    private void AddLabel(string text, double x, double y, Brush foreground)
    {
        TextBlock label = new()
        {
            Text = text,
            Foreground = foreground,
            FontSize = 14,
            FontWeight = FontWeights.Bold,
            Background = new SolidColorBrush(Color.FromArgb(150, 0, 0, 0)),
            Padding = new Thickness(3, 1, 3, 1)
        };
        Canvas.SetLeft(label, x);
        Canvas.SetTop(label, y);
        OverlayCanvas.Children.Add(label);
    }

    private enum DragTarget
    {
        None,
        A,
        B
    }

}
