using System.Media;
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
    private const double HandleEdgePadding = RulerRenderer.HandleEdgePadding;

    private double _pointAX = 400;
    private double _pointAY = 700;
    private double _pointBX = 650;
    private double _pointBY = 300;
    private readonly List<FibLevel> _levels = FibLevel.CreateDefaults().ToList();
    private readonly HotkeyService _hotkeyService = new();
    private readonly SettingsService _settingsService = new();
    private readonly RulerRenderer _renderer;
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
    private ContextMenu? _fibLevelsMenu;
    private bool _isToolEnabled;
    private ToggleShortcutSettings _drawShortcut = ShortcutValidator.CreateDefaultDraw();
    private ToggleShortcutSettings _deleteShortcut = ShortcutValidator.CreateDefaultDelete();
    private ColorSettings _colors = ColorSettings.CreateDefault();
    private double _bandOpacity = ColorSettings.DefaultBandOpacity;
    private AppSettings? _startupSettings;

    public bool IsToolEnabled => _isToolEnabled;
    public string DrawShortcutDisplay => _hotkeyService.DrawShortcutDisplay;
    public string DeleteShortcutDisplay => _hotkeyService.DeleteShortcutDisplay;
    public ToggleShortcutSettings DrawShortcutSettings => _hotkeyService.DrawShortcut;
    public ToggleShortcutSettings DeleteShortcutSettings => _hotkeyService.DeleteShortcut;
    public ColorSettings CurrentColors => ColorSettings.Clone(_colors);
    public double CurrentBandOpacity => _bandOpacity;
    public event EventHandler? ToolEnabledChanged;
    public event EventHandler<HotkeyRegistrationFailedEventArgs>? HotkeyRegistrationFailed;

    public MainWindow()
    {
        InitializeComponent();
        _renderer = new RulerRenderer(OverlayCanvas);
        _renderer.DeleteRequested += (_, _) => DeleteCurrentRuler();
        SourceInitialized += MainWindow_SourceInitialized;
        Closed += MainWindow_Closed;
        _hotkeyService.EditLockHotkeyPressed += HotkeyService_EditLockHotkeyPressed;
        _hotkeyService.EscapePressed += HotkeyService_EscapePressed;
        _hotkeyService.DrawHotkeyPressed += HotkeyService_DrawHotkeyPressed;
        _hotkeyService.DeleteHotkeyPressed += HotkeyService_DeleteHotkeyPressed;
        _hotkeyService.RegistrationFailed += HotkeyService_RegistrationFailed;
        _startupSettings = _settingsService.Load();
        ToggleShortcutSettings drawShortcut = _drawShortcut;
        ToggleShortcutSettings deleteShortcut = _deleteShortcut;
        bool hasValidDrawShortcut = _startupSettings?.DrawShortcut is not null
            && ShortcutValidator.TryParse(_startupSettings.DrawShortcut, out drawShortcut);
        bool hasValidDeleteShortcut = _startupSettings?.DeleteShortcut is not null
            && ShortcutValidator.TryParse(_startupSettings.DeleteShortcut, out deleteShortcut);
        if (hasValidDrawShortcut && hasValidDeleteShortcut
            && ShortcutValidator.TryValidateEditShortcuts(drawShortcut, deleteShortcut, out _))
        {
            _drawShortcut = drawShortcut;
            _deleteShortcut = deleteShortcut;
        }

        RestoreAppearanceSettings(_startupSettings);
    }

    private void MainWindow_SourceInitialized(object? sender, EventArgs e)
    {
        _hotkeyService.SetEditShortcuts(_drawShortcut, _deleteShortcut);
        if (!_hotkeyService.Register(this))
        {
            System.Diagnostics.Debug.WriteLine("Fib Ruler: いずれかのグローバルホットキーを登録できませんでした。");
        }

        ApplyMode(RulerMode.Edit);
        _hotkeyService.SetEditLockEnabled(false);
    }

    private void MainWindow_Closed(object? sender, EventArgs e)
    {
        if (_drawingMode is DrawingMode.Armed or DrawingMode.Drawing)
        {
            CancelDrawing(playSound: false);
        }

        SaveSettings();
        _hotkeyService.Dispose();
    }

    private void HotkeyService_RegistrationFailed(object? sender, HotkeyRegistrationFailedEventArgs e) =>
        HotkeyRegistrationFailed?.Invoke(this, e);

    private void HotkeyService_EditLockHotkeyPressed(object? sender, EventArgs e)
    {
        if (!_isToolEnabled)
        {
            return;
        }

        ApplyMode(_mode == RulerMode.Edit ? RulerMode.Locked : RulerMode.Edit);
    }

    private void HotkeyService_DrawHotkeyPressed(object? sender, EventArgs e)
    {
        if (_isToolEnabled && _mode == RulerMode.Edit)
        {
            ToggleDrawMode();
        }
    }

    private void HotkeyService_DeleteHotkeyPressed(object? sender, EventArgs e)
    {
        if (_isToolEnabled && _mode == RulerMode.Edit
            && _drawingMode == DrawingMode.Placed && _hasPlacedRuler)
        {
            DeleteCurrentRuler();
        }
    }

    private void ToggleDrawMode()
    {
        if (!_isToolEnabled || _mode != RulerMode.Edit)
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
        UpdateEscapeHotkey();
        ApplyInteractionState();
        UpdateRulerVisuals();
        PlayDrawModeSound(isOn: true);
    }

    private void HotkeyService_EscapePressed(object? sender, EventArgs e)
    {
        if (!_isToolEnabled)
        {
            return;
        }

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
            CloseFibLevelsMenu();
            _dragTarget = DragTarget.None;
            if (OverlayCanvas.IsMouseCaptured)
            {
                OverlayCanvas.ReleaseMouseCapture();
            }

            if (_drawingMode is DrawingMode.Armed or DrawingMode.Drawing)
            {
                CancelDrawing();
            }
        }

        ApplyInteractionState();
        UpdateRulerVisuals();
        UpdateEscapeHotkey();
        _hotkeyService.SetEditActionsEnabled(_isToolEnabled && _mode == RulerMode.Edit);
    }

    private void ApplyInteractionState()
    {
        bool drawingModeActive = _drawingMode is DrawingMode.Armed or DrawingMode.Drawing;
        bool clickThrough = !_isToolEnabled || _mode == RulerMode.Locked || (_drawingMode == DrawingMode.Idle && !_hasPlacedRuler);
        Topmost = _isToolEnabled && (_hasPlacedRuler || drawingModeActive || _mode == RulerMode.Locked);

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
        RestoreSettings(_startupSettings);
        _startupSettings = null;
        _isToolEnabled = false;
        _drawingMode = _hasPlacedRuler ? DrawingMode.Placed : DrawingMode.Idle;
        ApplyInteractionState();
        UpdateRulerVisuals();
    }

    public void ToggleToolEnabled() => SetToolEnabled(!_isToolEnabled);

    public bool TryChangeEditShortcuts(
        ToggleShortcutSettings draw,
        ToggleShortcutSettings delete,
        out string error)
    {
        if (!_hotkeyService.TryChangeEditShortcuts(draw, delete, out error))
        {
            return false;
        }

        _drawShortcut = _hotkeyService.DrawShortcut;
        _deleteShortcut = _hotkeyService.DeleteShortcut;
        SaveSettings();
        return true;
    }

    public bool TryApplyAppearance(ColorSettings colors, double bandOpacity, out string error)
    {
        error = string.Empty;
        foreach (string key in ColorSettings.ColorKeys)
        {
            if (!ColorSettings.TryNormalizeHex(colors.GetColor(key), out _))
            {
                error = "色は #RRGGBB 形式で入力してください。";
                return false;
            }
        }

        if (!double.IsFinite(bandOpacity) || bandOpacity is < 0 or > 1)
        {
            error = "帯の透明度は0%から100%の範囲で指定してください。";
            return false;
        }

        ColorSettings normalized = ColorSettings.Clone(colors);
        foreach (string key in ColorSettings.ColorKeys)
        {
            ColorSettings.TryNormalizeHex(colors.GetColor(key), out string value);
            normalized.SetColor(key, value);
        }

        _colors = normalized;
        _bandOpacity = bandOpacity;
        UpdateRulerVisuals();
        SaveSettings();
        return true;
    }

    public void ResetAppearance()
    {
        _colors = ColorSettings.CreateDefault();
        _bandOpacity = ColorSettings.DefaultBandOpacity;
        UpdateRulerVisuals();
        SaveSettings();
    }

    private void SetToolEnabled(bool enabled)
    {
        if (_isToolEnabled == enabled)
        {
            return;
        }

        _isToolEnabled = enabled;
        if (!enabled)
        {
            if (_drawingMode is DrawingMode.Armed or DrawingMode.Drawing)
            {
                CancelDrawing(playSound: false);
            }

            CloseFibLevelsMenu();
            _dragTarget = DragTarget.None;
            if (OverlayCanvas.IsMouseCaptured)
            {
                OverlayCanvas.ReleaseMouseCapture();
            }

            OverlayCanvas.Cursor = Cursors.Arrow;
            _hotkeyService.SetEditLockEnabled(false);
        }
        else
        {
            _hotkeyService.SetEditLockEnabled(true);
        }

        UpdateEscapeHotkey();
        ApplyInteractionState();
        UpdateRulerVisuals();
        _hotkeyService.SetEditActionsEnabled(_isToolEnabled && _mode == RulerMode.Edit);
        ToolEnabledChanged?.Invoke(this, EventArgs.Empty);
    }

    private void Window_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (IsLoaded) UpdateRulerVisuals();
    }

    private void OverlayCanvas_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (!_isToolEnabled || _mode != RulerMode.Edit)
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
        if (_isToolEnabled && _mode == RulerMode.Edit
            && _drawingMode == DrawingMode.Drawing
            && e.LeftButton == MouseButtonState.Pressed)
        {
            UpdateDrawingPreview(e.GetPosition(OverlayCanvas));
            e.Handled = true;
            return;
        }

        if (!_isToolEnabled || _mode != RulerMode.Edit || _dragTarget == DragTarget.None || e.LeftButton != MouseButtonState.Pressed)
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
        if (!_isToolEnabled || _mode != RulerMode.Edit
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

        if (_fibLevelsMenu is { IsOpen: true })
        {
            _fibLevelsMenu.IsOpen = false;
        }

        ContextMenu menu = CreateFibLevelsMenu();
        _fibLevelsMenu = menu;
        menu.Closed += FibLevelsMenu_Closed;
        menu.PlacementTarget = OverlayCanvas;
        menu.IsOpen = true;
        e.Handled = true;
    }

    private void FibLevelsMenu_Closed(object? sender, RoutedEventArgs e)
    {
        if (sender is ContextMenu menu && ReferenceEquals(_fibLevelsMenu, menu))
        {
            _fibLevelsMenu = null;
        }
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
        UpdateEscapeHotkey();
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
        UpdateEscapeHotkey();
        if (OverlayCanvas.IsMouseCaptured)
        {
            OverlayCanvas.ReleaseMouseCapture();
        }

        ApplyInteractionState();
        UpdateRulerVisuals();
        SaveSettings();
        PlayDrawModeSound(isOn: false);
    }

    private void CancelDrawing(bool playSound = true)
    {
        if (_drawingMode != DrawingMode.Drawing && _drawingMode != DrawingMode.Armed)
        {
            return;
        }

        if (OverlayCanvas.IsMouseCaptured)
        {
            OverlayCanvas.ReleaseMouseCapture();
        }

        OverlayCanvas.Cursor = Cursors.Arrow;
        _pointAX = _drawingOriginalA.X;
        _pointAY = _drawingOriginalA.Y;
        _pointBX = _drawingOriginalB.X;
        _pointBY = _drawingOriginalB.Y;
        _hasPlacedRuler = _drawingHadPlacedRuler;

        _drawingMode = _hasPlacedRuler ? DrawingMode.Placed : DrawingMode.Idle;
        UpdateEscapeHotkey();
        ApplyInteractionState();
        UpdateRulerVisuals();
        if (playSound)
        {
            PlayDrawModeSound(isOn: false);
        }
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

    private void RestoreSettings(AppSettings? settings)
    {
        if (settings is null)
        {
            return;
        }

        bool hasValidPointA = IsFinitePoint(settings.PointA);
        bool hasValidPointB = IsFinitePoint(settings.PointB);
        _hasPlacedRuler = settings.HasPlacedRuler && hasValidPointA && hasValidPointB;

        if (hasValidPointA)
        {
            Point pointA = ClampPointToOverlayBounds(new Point(settings.PointA.X, settings.PointA.Y));
            _pointAX = pointA.X;
            _pointAY = pointA.Y;
        }

        if (hasValidPointB)
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
            HasPlacedRuler = _hasPlacedRuler,
            FibLevels = _levels.Select(level => new FibLevelSettings
            {
                Ratio = level.Ratio,
                Enabled = level.Enabled,
                IsCustom = !FibLevel.IsDefaultRatio(level.Ratio)
            }).ToList(),
            DrawShortcut = _hotkeyService.DrawShortcut,
            DeleteShortcut = _hotkeyService.DeleteShortcut,
            Colors = ColorSettings.Clone(_colors),
            BandOpacity = _bandOpacity
        };

        _settingsService.Save(settings);
    }

    private static bool IsFinitePoint(PointSettings? point) =>
        point is not null && double.IsFinite(point.X) && double.IsFinite(point.Y);

    private void RestoreAppearanceSettings(AppSettings? settings)
    {
        if (settings?.Colors is not null)
        {
            ColorSettings restoredColors = ColorSettings.CreateDefault();
            foreach (string key in ColorSettings.ColorKeys)
            {
                string value = settings.Colors.GetColor(key);
                restoredColors.SetColor(
                    key,
                    ColorSettings.TryNormalizeHex(value, out string normalized)
                        ? normalized
                        : restoredColors.GetColor(key));
            }

            _colors = restoredColors;
        }

        if (settings?.BandOpacity is double bandOpacity && double.IsFinite(bandOpacity))
        {
            _bandOpacity = Math.Clamp(bandOpacity, 0, 1);
        }
    }

    private void UpdateRulerVisuals() =>
        _renderer.Render(new RulerRenderState(
            _isToolEnabled,
            _hasPlacedRuler,
            _mode == RulerMode.Edit && _drawingMode == DrawingMode.Placed,
            _pointAX,
            _pointAY,
            _pointBX,
            _pointBY,
            _levels,
            _colors,
            _bandOpacity));

    private void DeleteCurrentRuler()
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
        UpdateEscapeHotkey();
        ApplyInteractionState();
        UpdateRulerVisuals();
        SaveSettings();
    }

    private void UpdateEscapeHotkey()
    {
        bool enabled = _isToolEnabled && _mode == RulerMode.Edit
            && (_drawingMode is DrawingMode.Armed or DrawingMode.Drawing);
        _hotkeyService.SetEscapeEnabled(enabled);
    }

    private void CloseFibLevelsMenu()
    {
        if (_fibLevelsMenu is { IsOpen: true })
        {
            _fibLevelsMenu.IsOpen = false;
        }

        _fibLevelsMenu = null;
    }

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
        double edgePadding = HandleEdgePadding;
        return (
            edgePadding,
            Math.Max(edgePadding, ActualWidth - edgePadding),
            edgePadding,
            Math.Max(edgePadding, ActualHeight - edgePadding));
    }
}
