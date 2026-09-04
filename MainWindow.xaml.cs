using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Media;
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
    private const double DeleteButtonSize = 24;

    private double _pointAX = 400;
    private double _pointAY = 700;
    private double _pointBX = 650;
    private double _pointBY = 300;
    private readonly List<FibLevel> _levels = FibLevel.CreateDefaults().ToList();
    private readonly HotkeyService _hotkeyService = new();
    private readonly KeyboardHookService _keyboardHookService = new();
    private readonly SettingsService _settingsService = new();
    private RulerMode _mode = RulerMode.Edit;
    private DrawingMode _drawingMode = DrawingMode.Idle;
    private bool _hasPlacedRuler;
    private DragTarget _dragTarget;
    private Point _wholeDragStartMouse;
    private Point _wholeDragStartA;
    private Point _wholeDragStartB;
    private Point _drawingOriginalA;
    private Point _drawingOriginalB;
    private bool _drawingHadPlacedRuler;

    public MainWindow()
    {
        InitializeComponent();
        SourceInitialized += MainWindow_SourceInitialized;
        Closed += MainWindow_Closed;
        _hotkeyService.EditLockHotkeyPressed += HotkeyService_EditLockHotkeyPressed;
        _hotkeyService.EscapePressed += HotkeyService_EscapePressed;
        _keyboardHookService.ShiftTapped += KeyboardHookService_ShiftTapped;
    }

    private void MainWindow_SourceInitialized(object? sender, EventArgs e)
    {
        if (!_hotkeyService.Register(this))
        {
            System.Diagnostics.Debug.WriteLine("Fib Ruler: いずれかのグローバルホットキーを登録できませんでした。");
        }

        if (!_keyboardHookService.Start())
        {
            System.Diagnostics.Debug.WriteLine("Fib Ruler: Shift単押し検出を開始できませんでした。");
        }

        ApplyMode(RulerMode.Edit);
    }

    private void MainWindow_Closed(object? sender, EventArgs e)
    {
        SaveSettings();
        _hotkeyService.Dispose();
        _keyboardHookService.Dispose();
    }

    private void HotkeyService_EditLockHotkeyPressed(object? sender, EventArgs e)
    {
        ApplyMode(_mode == RulerMode.Edit ? RulerMode.Locked : RulerMode.Edit);
    }

    private void KeyboardHookService_ShiftTapped(object? sender, EventArgs e)
    {
        _ = Dispatcher.BeginInvoke(
            System.Windows.Threading.DispatcherPriority.Input,
            new Action(ToggleDrawMode));
    }

    private void ToggleDrawMode()
    {
        if (_mode != RulerMode.Edit)
        {
            return;
        }

        if (_drawingMode is DrawingMode.Armed or DrawingMode.Drawing)
        {
            CancelDrawing();
            return;
        }

        _drawingHadPlacedRuler = _hasPlacedRuler;
        _drawingOriginalA = new Point(_pointAX, _pointAY);
        _drawingOriginalB = new Point(_pointBX, _pointBY);
        _drawingMode = DrawingMode.Armed;
        _hotkeyService.SetEscapeEnabled(true);
        ApplyInteractionState();
        UpdateRulerVisuals();
        PlayDrawModeSound(isOn: true);
    }

    private void HotkeyService_EscapePressed(object? sender, EventArgs e)
    {
        if (_drawingMode is DrawingMode.Armed or DrawingMode.Drawing)
        {
            CancelDrawing();
        }
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

            if (_drawingMode is DrawingMode.Armed or DrawingMode.Drawing)
            {
                CancelDrawing();
                return;
            }
        }

        ApplyInteractionState();
        UpdateRulerVisuals();
    }

    private void ApplyInteractionState()
    {
        bool drawingModeActive = _drawingMode is DrawingMode.Armed or DrawingMode.Drawing;
        bool clickThrough = _mode == RulerMode.Locked || (_drawingMode == DrawingMode.Idle && !_hasPlacedRuler);

        OverlayCanvas.Background = drawingModeActive
            ? new SolidColorBrush(Color.FromArgb(1, 0, 0, 0))
            : Brushes.Transparent;

        try
        {
            WindowInteropService.SetClickThrough(this, clickThrough);
        }
        catch (Exception exception)
        {
            System.Diagnostics.Debug.WriteLine($"Fib Ruler: モード切替に失敗しました。{exception}");
        }

    }

    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
        RestoreSettings();
        _drawingMode = DrawingMode.Idle;
        _hasPlacedRuler = false;
        ApplyInteractionState();
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

        if (_drawingMode == DrawingMode.Armed)
        {
            BeginDrawing(e.GetPosition(OverlayCanvas));
            OverlayCanvas.CaptureMouse();
            e.Handled = true;
            return;
        }

        if (!_hasPlacedRuler || _drawingMode == DrawingMode.Drawing)
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
        if (_mode == RulerMode.Edit
            && _drawingMode == DrawingMode.Drawing
            && e.LeftButton == MouseButtonState.Pressed)
        {
            UpdateDrawingPreview(e.GetPosition(OverlayCanvas));
            e.Handled = true;
            return;
        }

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
        if (_drawingMode == DrawingMode.Drawing)
        {
            CompleteDrawing(e.GetPosition(OverlayCanvas));
            e.Handled = true;
            return;
        }

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
        if (_mode != RulerMode.Edit
            || _drawingMode is DrawingMode.Armed or DrawingMode.Drawing
            || !_hasPlacedRuler
            || !IsContextMenuTarget(e.OriginalSource))
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

    private void BeginDrawing(Point overlayPosition)
    {
        if (_mode != RulerMode.Edit || _drawingMode != DrawingMode.Armed)
        {
            return;
        }

        _drawingHadPlacedRuler = _hasPlacedRuler;
        _drawingOriginalA = new Point(_pointAX, _pointAY);
        _drawingOriginalB = new Point(_pointBX, _pointBY);
        Point drawingStart = ClampPointToOverlayBounds(overlayPosition);
        _pointAX = drawingStart.X;
        _pointAY = drawingStart.Y;
        _pointBX = drawingStart.X;
        _pointBY = drawingStart.Y;
        _hasPlacedRuler = true;
        _drawingMode = DrawingMode.Drawing;
        _hotkeyService.SetEscapeEnabled(true);
        ApplyInteractionState();
        UpdateRulerVisuals();
    }

    private void UpdateDrawingPreview(Point overlayPosition)
    {
        if (_mode != RulerMode.Edit || _drawingMode != DrawingMode.Drawing)
        {
            return;
        }

        Point position = ClampPointToOverlayBounds(overlayPosition);
        _pointBX = position.X;
        _pointBY = position.Y;
        UpdateRulerVisuals();
    }

    private void CompleteDrawing(Point overlayPosition)
    {
        if (_mode != RulerMode.Edit || _drawingMode != DrawingMode.Drawing)
        {
            return;
        }

        Point position = ClampPointToOverlayBounds(overlayPosition);
        _pointBX = position.X;
        _pointBY = position.Y;
        _drawingMode = DrawingMode.Placed;
        _hotkeyService.SetEscapeEnabled(false);
        if (OverlayCanvas.IsMouseCaptured)
        {
            OverlayCanvas.ReleaseMouseCapture();
        }

        ApplyInteractionState();
        UpdateRulerVisuals();
        SaveSettings();
        PlayDrawModeSound(isOn: false);
    }

    private void CancelDrawing()
    {
        if (_drawingMode != DrawingMode.Drawing && _drawingMode != DrawingMode.Armed)
        {
            return;
        }

        _hotkeyService.SetEscapeEnabled(false);
        if (OverlayCanvas.IsMouseCaptured)
        {
            OverlayCanvas.ReleaseMouseCapture();
        }

        OverlayCanvas.Cursor = Cursors.Arrow;
        if (_drawingMode == DrawingMode.Drawing && _drawingHadPlacedRuler)
        {
            _pointAX = _drawingOriginalA.X;
            _pointAY = _drawingOriginalA.Y;
            _pointBX = _drawingOriginalB.X;
            _pointBY = _drawingOriginalB.Y;
            _hasPlacedRuler = true;
        }
        else if (!_drawingHadPlacedRuler)
        {
            _hasPlacedRuler = false;
        }

        _drawingMode = _hasPlacedRuler ? DrawingMode.Placed : DrawingMode.Idle;
        ApplyInteractionState();
        UpdateRulerVisuals();
        PlayDrawModeSound(isOn: false);
    }

    private static void PlayDrawModeSound(bool isOn)
    {
        try
        {
            (isOn ? SystemSounds.Asterisk : SystemSounds.Exclamation).Play();
        }
        catch (Exception exception)
        {
            System.Diagnostics.Debug.WriteLine($"Fib Ruler: Draw Mode音の再生に失敗しました。{exception}");
        }
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

        MenuItem addItem = new()
        {
            Header = "Add Level..."
        };
        addItem.Click += AddFibLevelMenuItem_Click;
        menu.Items.Add(addItem);
        menu.Items.Add(new Separator());

        foreach (FibLevel level in _levels.OrderBy(item => item.Ratio))
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

        menu.Items.Add(new Separator());
        MenuItem deleteMenu = new()
        {
            Header = "Delete Level",
            IsEnabled = _levels.Count > 0
        };
        foreach (FibLevel level in _levels.OrderBy(item => item.Ratio))
        {
            MenuItem deleteItem = new()
            {
                Header = level.Label,
                Tag = level
            };
            deleteItem.Click += DeleteFibLevelMenuItem_Click;
            deleteMenu.Items.Add(deleteItem);
        }

        menu.Items.Add(deleteMenu);

        return menu;
    }

    private void AddFibLevelMenuItem_Click(object sender, RoutedEventArgs e)
    {
        AddFibLevelWindow dialog = new() { Owner = this };
        if (dialog.ShowDialog() != true)
        {
            return;
        }

        if (_levels.Any(level => Math.Abs(level.Ratio - dialog.Ratio) <= 0.0001))
        {
            MessageBox.Show(
                this,
                "そのFibレベルは既に存在します。",
                "Duplicate Fib Level",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            return;
        }

        _levels.Add(FibLevel.Create(dialog.Ratio));
        _levels.Sort(static (left, right) => left.Ratio.CompareTo(right.Ratio));
        UpdateRulerVisuals();
        SaveSettings();
    }

    private void DeleteFibLevelMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not MenuItem { Tag: FibLevel level } || !_levels.Contains(level))
        {
            return;
        }

        MessageBoxResult result = MessageBox.Show(
            this,
            $"Delete {level.Label}?",
            "Delete Fib Level",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);
        if (result != MessageBoxResult.Yes)
        {
            return;
        }

        _levels.Remove(level);
        UpdateRulerVisuals();
        SaveSettings();
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

        if (settings.FibLevels.Count == 0)
        {
            _levels.Clear();
            return;
        }

        List<FibLevel> restoredLevels = [];
        foreach (FibLevelSettings savedLevel in settings.FibLevels)
        {
            if (!double.IsFinite(savedLevel.Ratio))
            {
                continue;
            }

            if (savedLevel.Ratio < 0 && !savedLevel.IsCustom)
            {
                continue;
            }

            double ratio = FibLevel.NormalizeRatio(savedLevel.Ratio);
            if (restoredLevels.Any(level => Math.Abs(level.Ratio - ratio) <= 0.0001))
            {
                continue;
            }

            restoredLevels.Add(FibLevel.Create(ratio, savedLevel.Enabled));
        }

        if (restoredLevels.Count > 0)
        {
            _levels.Clear();
            _levels.AddRange(restoredLevels.OrderBy(level => level.Ratio));
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
                Enabled = level.Enabled,
                IsCustom = !FibLevel.IsDefaultRatio(level.Ratio)
            }).ToList()
        };

        _settingsService.Save(settings);
    }

    private static bool IsFinitePoint(PointSettings? point) =>
        point is not null && double.IsFinite(point.X) && double.IsFinite(point.Y);

    private void UpdateRulerVisuals()
    {
        OverlayCanvas.Children.Clear();
        if (!_hasPlacedRuler)
        {
            return;
        }

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
            new SolidColorBrush(Color.FromArgb(179, 120, 123, 134)), 1.1);
        AddWholeRulerHitArea();
        AddFibLabels(leftX);
        Brush anchorBrush = new SolidColorBrush(Color.FromRgb(41, 98, 255));
        AddPoint(_pointAX, _pointAY, anchorBrush, DragTarget.A);
        AddPoint(_pointBX, _pointBY, anchorBrush, DragTarget.B);
        if (_mode == RulerMode.Edit && _drawingMode == DrawingMode.Placed)
        {
            AddDeleteButton(rightX);
        }
    }

    private void AddDeleteButton(double rightX)
    {
        Button deleteButton = new()
        {
            Content = "×",
            Width = DeleteButtonSize,
            Height = DeleteButtonSize,
            Padding = new Thickness(0),
            FontSize = 16,
            FontWeight = FontWeights.SemiBold,
            Foreground = Brushes.White,
            Background = new SolidColorBrush(Color.FromArgb(115, 30, 35, 45)),
            BorderBrush = new SolidColorBrush(Color.FromArgb(150, 180, 210, 235)),
            BorderThickness = new Thickness(1),
            Cursor = Cursors.Hand,
            ToolTip = "Delete Fib ruler"
        };
        deleteButton.Click += DeleteRulerButton_Click;
        Canvas.SetLeft(deleteButton, Math.Max(ScreenMargin, rightX - DeleteButtonSize));
        Canvas.SetTop(deleteButton, ScreenMargin);
        OverlayCanvas.Children.Add(deleteButton);
    }

    private void DeleteRulerButton_Click(object sender, RoutedEventArgs e)
    {
        if (_mode != RulerMode.Edit || !_hasPlacedRuler || _drawingMode != DrawingMode.Placed)
        {
            return;
        }

        if (OverlayCanvas.IsMouseCaptured)
        {
            OverlayCanvas.ReleaseMouseCapture();
        }

        _dragTarget = DragTarget.None;
        OverlayCanvas.Cursor = Cursors.Arrow;
        _drawingMode = DrawingMode.Idle;
        _hasPlacedRuler = false;
        ApplyInteractionState();
        UpdateRulerVisuals();
        SaveSettings();
        e.Handled = true;
    }

    private void AddFibBands(double leftX, double rulerWidth)
    {
        (double Lower, double Upper)[] bands =
        [
            (0, 0.236),
            (0.236, 0.382),
            (0.382, 0.500),
            (0.500, 0.618),
            (0.618, 0.786),
            (0.786, 1.000),
            (1.000, 1.272),
            (1.272, 1.414),
            (1.414, 1.618),
            (1.618, 2.000),
            (2.000, 2.618),
            (2.618, 3.618),
            (3.618, 4.236)
        ];

        foreach ((double lower, double upper) in bands)
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
                Fill = new SolidColorBrush(Color.FromArgb(51, GetLevelColor(upper).R, GetLevelColor(upper).G, GetLevelColor(upper).B)),
                IsHitTestVisible = false
            };
            Canvas.SetLeft(band, leftX);
            Canvas.SetTop(band, Math.Min(firstY, secondY));
            OverlayCanvas.Children.Add(band);
        }
    }

    private bool IsLevelEnabled(double ratio) =>
        _levels.Any(level => Math.Abs(level.Ratio - ratio) <= 0.0001 && level.Enabled);

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

    private static Brush GetLevelBrush(double ratio) => new SolidColorBrush(GetLevelColor(ratio));

    private static Color GetLevelColor(double ratio) => ratio switch
    {
        0 or 1 => Color.FromRgb(128, 128, 128),
        0.236 or 2.000 or 2.618 => Color.FromRgb(242, 54, 69),
        0.382 or 0.618 or 1.272 => Color.FromRgb(76, 175, 80),
        0.500 => Color.FromRgb(255, 152, 0),
        0.786 or 1.414 => Color.FromRgb(0, 188, 212),
        1.618 => Color.FromRgb(41, 98, 255),
        3.618 => Color.FromRgb(156, 39, 176),
        4.236 => Color.FromRgb(233, 30, 99),
        _ when ratio < 0 => Color.FromRgb(120, 123, 134),
        _ => Color.FromRgb(76, 175, 80)
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
            Fill = Brushes.Transparent,
            Stroke = brush,
            StrokeThickness = 2,
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
