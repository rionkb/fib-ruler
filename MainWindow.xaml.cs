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
    private const double MinimumRulerWidth = 140;
    private const double LabelWidth = 44;
    private const double LabelMargin = 5;
    private const double HandleDiameter = 12;
    private const double HandleEdgeMargin = 2;
    private const double ScreenMargin = 6;

    private double _pointAX = 400;
    private double _pointAY = 700;
    private double _pointBX = 650;
    private double _pointBY = 300;
    private readonly IReadOnlyList<FibLevel> _levels = FibLevel.CreateDefaults();
    private readonly HotkeyService _hotkeyService = new();
    private readonly SettingsService _settingsService = new();
    private RulerMode _mode = RulerMode.Edit;
    private DragTarget _dragTarget;
    private Point _wholeDragStartMouse;
    private Point _wholeDragStartA;
    private Point _wholeDragStartB;

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
        SaveSettings();
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

    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
        RestoreSettings();
        UpdateRulerVisuals();
    }

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

        if (e.OriginalSource is Ellipse handle && handle.Tag is DragTarget target)
        {
            _dragTarget = target;
            OverlayCanvas.CaptureMouse();
            e.Handled = true;
            return;
        }

        if (e.OriginalSource is Line line && line.Tag is DragTarget.WholeRuler)
        {
            _dragTarget = DragTarget.WholeRuler;
            _wholeDragStartMouse = e.GetPosition(OverlayCanvas);
            _wholeDragStartA = new Point(_pointAX, _pointAY);
            _wholeDragStartB = new Point(_pointBX, _pointBY);
            OverlayCanvas.Cursor = Cursors.SizeAll;
            OverlayCanvas.CaptureMouse();
            e.Handled = true;
            return;
        }

        if (_dragTarget != DragTarget.None)
        {
            _dragTarget = DragTarget.None;
        }
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
            position = ClampPointToOverlayBounds(position);
            _pointAX = position.X;
            _pointAY = position.Y;
        }
        else if (_dragTarget == DragTarget.B)
        {
            position = ClampPointToOverlayBounds(position);
            _pointBX = position.X;
            _pointBY = position.Y;
        }
        else if (_dragTarget == DragTarget.WholeRuler)
        {
            Point delta = ClampWholeRulerDelta(position - _wholeDragStartMouse);
            _pointAX = _wholeDragStartA.X + delta.X;
            _pointAY = _wholeDragStartA.Y + delta.Y;
            _pointBX = _wholeDragStartB.X + delta.X;
            _pointBY = _wholeDragStartB.Y + delta.Y;
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
        OverlayCanvas.Cursor = Cursors.Arrow;
        SaveSettings();

        e.Handled = true;
    }

    private void OverlayCanvas_MouseRightButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (_mode != RulerMode.Edit || !IsContextMenuTarget(e.OriginalSource))
        {
            return;
        }

        _dragTarget = DragTarget.None;
        if (OverlayCanvas.IsMouseCaptured)
        {
            OverlayCanvas.ReleaseMouseCapture();
        }

        ContextMenu menu = CreateFibLevelsMenu();
        menu.PlacementTarget = OverlayCanvas;
        menu.IsOpen = true;
        e.Handled = true;
    }

    private static bool IsContextMenuTarget(object source) =>
        source is Ellipse handle && handle.Tag is DragTarget.A or DragTarget.B
        || source is Line line && line.Tag is DragTarget.WholeRuler;

    private ContextMenu CreateFibLevelsMenu()
    {
        ContextMenu menu = new();
        menu.Items.Add(new MenuItem
        {
            Header = "Fib Levels",
            IsEnabled = false
        });

        foreach (FibLevel level in _levels)
        {
            MenuItem item = new()
            {
                Header = level.Label,
                IsCheckable = true,
                IsChecked = level.Enabled,
                Tag = level
            };
            item.Click += FibLevelMenuItem_Click;
            menu.Items.Add(item);
        }

        return menu;
    }

    private void FibLevelMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (sender is MenuItem { Tag: FibLevel level } item)
        {
            level.Enabled = item.IsChecked;
            UpdateRulerVisuals();
            SaveSettings();
        }
    }

    private void RestoreSettings()
    {
        AppSettings? settings = _settingsService.Load();
        if (settings is null)
        {
            return;
        }

        if (IsFinitePoint(settings.PointA))
        {
            Point pointA = ClampPointToOverlayBounds(new Point(settings.PointA.X, settings.PointA.Y));
            _pointAX = pointA.X;
            _pointAY = pointA.Y;
        }

        if (IsFinitePoint(settings.PointB))
        {
            Point pointB = ClampPointToOverlayBounds(new Point(settings.PointB.X, settings.PointB.Y));
            _pointBX = pointB.X;
            _pointBY = pointB.Y;
        }

        foreach (FibLevelSettings savedLevel in settings.FibLevels)
        {
            FibLevel? level = _levels.FirstOrDefault(item => Math.Abs(item.Ratio - savedLevel.Ratio) <= 0.0001);
            if (level is not null)
            {
                level.Enabled = savedLevel.Enabled;
            }
        }
    }

    private void SaveSettings()
    {
        AppSettings settings = new()
        {
            SettingsVersion = 1,
            PointA = new PointSettings { X = _pointAX, Y = _pointAY },
            PointB = new PointSettings { X = _pointBX, Y = _pointBY },
            FibLevels = _levels.Select(level => new FibLevelSettings
            {
                Ratio = level.Ratio,
                Enabled = level.Enabled
            }).ToList()
        };

        _settingsService.Save(settings);
    }

    private static bool IsFinitePoint(PointSettings? point) =>
        point is not null && double.IsFinite(point.X) && double.IsFinite(point.Y);

    private void UpdateRulerVisuals()
    {
        OverlayCanvas.Children.Clear();

        double leftX = Math.Min(_pointAX, _pointBX);
        double rulerWidth = Math.Max(MinimumRulerWidth, Math.Abs(_pointBX - _pointAX));
        double rightX = Math.Max(_pointAX, _pointBX);
        if (rulerWidth > rightX - leftX)
        {
            double centerX = (leftX + rightX) / 2;
            leftX = centerX - rulerWidth / 2;
            rightX = centerX + rulerWidth / 2;
        }

        AddFibBands(leftX, rulerWidth);
        AddFibLines(leftX, rightX);
        AddLine(_pointAX, _pointAY, _pointBX, _pointBY,
            new SolidColorBrush(Color.FromArgb(150, 255, 255, 255)), 1);
        AddWholeRulerHitArea();
        AddFibLabels(leftX);
        AddPoint(_pointAX, _pointAY, Brushes.DodgerBlue, DragTarget.A);
        AddPoint(_pointBX, _pointBY, Brushes.OrangeRed, DragTarget.B);
        AddLabel("A", _pointAX + 6, _pointAY + 6, Brushes.DodgerBlue);
        AddLabel("B", _pointBX + 6, _pointBY - 18, Brushes.OrangeRed);
    }

    private void AddFibBands(double leftX, double rulerWidth)
    {
        (double Lower, double Upper, Color Color)[] bands =
        [
            (0, 0.236, Color.FromRgb(220, 80, 80)),
            (0.236, 0.382, Color.FromRgb(238, 145, 70)),
            (0.382, 0.500, Color.FromRgb(230, 195, 75)),
            (0.500, 0.618, Color.FromRgb(100, 180, 105)),
            (0.618, 0.786, Color.FromRgb(70, 155, 205)),
            (0.786, 1.000, Color.FromRgb(100, 125, 210)),
            (1.000, 1.272, Color.FromRgb(155, 105, 205)),
            (1.272, 1.414, Color.FromRgb(190, 95, 175)),
            (1.414, 1.618, Color.FromRgb(215, 100, 140)),
            (1.618, 2.000, Color.FromRgb(220, 125, 80)),
            (2.000, 2.618, Color.FromRgb(205, 165, 70)),
            (2.618, 3.618, Color.FromRgb(105, 170, 110)),
            (3.618, 4.236, Color.FromRgb(90, 145, 195))
        ];

        foreach ((double lower, double upper, Color color) in bands)
        {
            if (!IsLevelEnabled(lower) || !IsLevelEnabled(upper))
            {
                continue;
            }

            double firstY = FibCalculator.CalculateY(_pointAY, _pointBY, lower);
            double secondY = FibCalculator.CalculateY(_pointAY, _pointBY, upper);
            Rectangle band = new()
            {
                Width = rulerWidth,
                Height = Math.Abs(secondY - firstY),
                Fill = new SolidColorBrush(Color.FromArgb(26, color.R, color.G, color.B)),
                IsHitTestVisible = false
            };
            Canvas.SetLeft(band, leftX);
            Canvas.SetTop(band, Math.Min(firstY, secondY));
            OverlayCanvas.Children.Add(band);
        }
    }

    private bool IsLevelEnabled(double ratio) =>
        _levels.First(level => level.Ratio == ratio).Enabled;

    private void AddFibLines(double leftX, double rightX)
    {
        foreach (FibLevel level in _levels.Where(static item => item.Enabled))
        {
            double y = FibCalculator.CalculateY(_pointAY, _pointBY, level.Ratio);
            double thickness = level.Ratio is 0 or 1 ? 1.6 : 1.1;
            AddLine(leftX, y, rightX, y, GetLevelBrush(level.Ratio), thickness);
        }
    }

    private void AddFibLabels(double leftX)
    {
        double labelX = Math.Max(ScreenMargin, leftX - LabelWidth - LabelMargin);
        foreach (FibLevel level in _levels.Where(static item => item.Enabled))
        {
            double y = FibCalculator.CalculateY(_pointAY, _pointBY, level.Ratio);
            AddLabel(level.Label, labelX, y - 9, GetLevelBrush(level.Ratio), LabelWidth);
        }
    }

    private static Brush GetLevelBrush(double ratio) => ratio switch
    {
        0 or 1 => Brushes.White,
        0.5 or 0.618 => Brushes.Gold,
        1.618 or 2.618 or 4.236 => Brushes.Orchid,
        _ when ratio < 0 => Brushes.LightSteelBlue,
        _ => Brushes.LightGreen
    };

    private Point ClampPointToOverlayBounds(Point point)
    {
        (double minX, double maxX, double minY, double maxY) = GetHandleBounds();

        return new Point(
            Math.Clamp(point.X, minX, maxX),
            Math.Clamp(point.Y, minY, maxY));
    }

    private Point ClampWholeRulerDelta(Vector delta)
    {
        (double minX, double maxX, double minY, double maxY) = GetHandleBounds();
        double minDeltaX = Math.Max(minX - _wholeDragStartA.X, minX - _wholeDragStartB.X);
        double maxDeltaX = Math.Min(maxX - _wholeDragStartA.X, maxX - _wholeDragStartB.X);
        double minDeltaY = Math.Max(minY - _wholeDragStartA.Y, minY - _wholeDragStartB.Y);
        double maxDeltaY = Math.Min(maxY - _wholeDragStartA.Y, maxY - _wholeDragStartB.Y);

        return new Point(
            Math.Clamp(delta.X, minDeltaX, maxDeltaX),
            Math.Clamp(delta.Y, minDeltaY, maxDeltaY));
    }

    private (double MinX, double MaxX, double MinY, double MaxY) GetHandleBounds()
    {
        double edgePadding = HandleDiameter / 2 + HandleEdgeMargin;
        return (
            edgePadding,
            Math.Max(edgePadding, ActualWidth - edgePadding),
            edgePadding,
            Math.Max(edgePadding, ActualHeight - edgePadding));
    }

    private void AddWholeRulerHitArea()
    {
        OverlayCanvas.Children.Add(new Line
        {
            X1 = _pointAX,
            Y1 = _pointAY,
            X2 = _pointBX,
            Y2 = _pointBY,
            Stroke = Brushes.Transparent,
            StrokeThickness = 14,
            Tag = DragTarget.WholeRuler,
            Cursor = Cursors.SizeAll
        });
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
            SnapsToDevicePixels = true,
            IsHitTestVisible = false
        });
    }

    private void AddPoint(double x, double y, Brush brush, DragTarget target)
    {
        Ellipse point = new()
        {
            Width = HandleDiameter,
            Height = HandleDiameter,
            Fill = brush,
            Stroke = Brushes.White,
            StrokeThickness = 1,
            Tag = target,
            Cursor = Cursors.SizeAll
        };
        Canvas.SetLeft(point, x - HandleDiameter / 2);
        Canvas.SetTop(point, y - HandleDiameter / 2);
        OverlayCanvas.Children.Add(point);
    }

    private void AddLabel(string text, double x, double y, Brush foreground, double width = 24)
    {
        TextBlock label = new()
        {
            Text = text,
            Foreground = foreground,
            FontSize = width == LabelWidth ? 12 : 12,
            FontWeight = FontWeights.SemiBold,
            Width = width,
            TextAlignment = width == LabelWidth ? TextAlignment.Right : TextAlignment.Left,
            Padding = new Thickness(0, 1, 0, 1)
        };
        Canvas.SetLeft(label, x);
        Canvas.SetTop(label, y);
        OverlayCanvas.Children.Add(label);
    }

    private enum DragTarget
    {
        None,
        A,
        B,
        WholeRuler
    }

}
