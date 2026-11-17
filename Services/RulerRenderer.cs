using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using FibRuler.Models;

namespace FibRuler.Services;

internal readonly record struct RulerRenderState(
    bool ToolEnabled,
    bool HasPlacedRuler,
    bool ShowDeleteButton,
    double PointAX,
    double PointAY,
    double PointBX,
    double PointBY,
    IReadOnlyList<FibLevel> Levels,
    ColorSettings Colors,
    double BandOpacity);

/// <summary>
/// Fib定規をオーバーレイCanvasへ描画する。
/// 構成（レベル・色・モード）が変わった時だけ要素を再構築し、
/// ドラッグ中は既存要素の座標のみを更新することで再描画を軽量化する。
/// </summary>
internal sealed class RulerRenderer
{
    private const double LabelWidth = 44;
    private const double LabelMargin = 5;
    private const double HandleDiameter = 12;
    private const double HandleEdgeMargin = 2;
    internal const double HandleEdgePadding = HandleDiameter / 2 + HandleEdgeMargin;
    private const double ScreenMargin = 6;
    private const double DeleteButtonSize = 24;

    private const int BandZIndex = 0;
    private const int LineZIndex = 10;
    private const int AnchorLineZIndex = 20;
    private const int HitLineZIndex = 30;
    private const int LabelZIndex = 40;
    private const int HandleZIndex = 50;
    private const int DeleteButtonZIndex = 60;

    private static readonly (double Lower, double Upper)[] BandRanges =
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

    private static readonly Color RulerEdgeColor = Color.FromRgb(120, 123, 134);
    private static readonly Color AnchorHandleColor = Color.FromRgb(41, 98, 255);

    private readonly Canvas _canvas;
    private readonly Canvas _root;
    private readonly Line _anchorLine;
    private readonly Line _hitLine;
    private readonly Ellipse _handleA;
    private readonly Ellipse _handleB;
    private readonly Button _deleteButton;
    private readonly List<Rectangle> _bandRects = [];
    private readonly List<Line> _lines = [];
    private readonly List<TextBlock> _labels = [];
    private readonly List<FibLevel> _enabledLevels = [];
    private readonly List<(Rectangle Rect, double Lower, double Upper)> _activeBands = [];
    private readonly Dictionary<string, Color> _palette = [];
    private readonly Dictionary<Color, Brush> _brushCache = [];
    private string? _signature;

    public event EventHandler? DeleteRequested;

    public RulerRenderer(Canvas canvas)
    {
        _canvas = canvas;

        _root = new Canvas();
        Canvas.SetZIndex(_root, 0);
        _root.Visibility = Visibility.Collapsed;
        _canvas.Children.Add(_root);

        Brush anchorBrush = GetFrozenBrush(AnchorHandleColor);
        _anchorLine = new Line
        {
            Stroke = GetFrozenBrush(Color.FromArgb(179, RulerEdgeColor.R, RulerEdgeColor.G, RulerEdgeColor.B)),
            StrokeThickness = 1.1,
            SnapsToDevicePixels = true,
            IsHitTestVisible = false
        };
        Canvas.SetZIndex(_anchorLine, AnchorLineZIndex);
        _root.Children.Add(_anchorLine);

        _hitLine = new Line
        {
            Stroke = Brushes.Transparent,
            StrokeThickness = 14,
            Tag = DragTarget.WholeRuler,
            Cursor = Cursors.SizeAll
        };
        Canvas.SetZIndex(_hitLine, HitLineZIndex);
        _root.Children.Add(_hitLine);

        _handleA = CreateHandle(anchorBrush, DragTarget.A);
        _handleB = CreateHandle(anchorBrush, DragTarget.B);
        _root.Children.Add(_handleA);
        _root.Children.Add(_handleB);

        _deleteButton = CreateDeleteButton();
        Canvas.SetZIndex(_deleteButton, DeleteButtonZIndex);
        _root.Children.Add(_deleteButton);
    }

    public void Render(in RulerRenderState state)
    {
        bool showRuler = state.ToolEnabled && state.HasPlacedRuler;
        string signature = BuildSignature(in state, showRuler);
        if (signature != _signature)
        {
            _signature = signature;
            Rebuild(in state, showRuler);
        }

        _root.Visibility = showRuler ? Visibility.Visible : Visibility.Collapsed;
        if (!showRuler)
        {
            return;
        }

        UpdateGeometry(in state);
    }

    private static string BuildSignature(in RulerRenderState state, bool showRuler)
    {
        StringBuilder builder = new();
        builder.Append(showRuler)
            .Append('|').Append(state.ShowDeleteButton)
            .Append('|').Append(state.BandOpacity);
        foreach (string key in ColorSettings.ColorKeys)
        {
            builder.Append('|').Append(state.Colors.GetColor(key));
        }

        foreach (FibLevel level in state.Levels)
        {
            builder.Append('|').Append(level.Ratio).Append(level.Enabled ? 'y' : 'n');
        }

        return builder.ToString();
    }

    private void Rebuild(in RulerRenderState state, bool showRuler)
    {
        _palette.Clear();
        foreach (string key in ColorSettings.ColorKeys)
        {
            _palette[key] = ParseColor(state.Colors.GetColor(key));
        }

        _enabledLevels.Clear();
        _enabledLevels.AddRange(state.Levels.Where(static level => level.Enabled).OrderBy(static level => level.Ratio));

        RebuildLevelLines();
        RebuildBands(in state);

        _deleteButton.Visibility = showRuler && state.ShowDeleteButton ? Visibility.Visible : Visibility.Collapsed;
    }

    private void RebuildLevelLines()
    {
        int count = _enabledLevels.Count;
        EnsureCount(_lines, count, CreateLevelLine, LineZIndex);
        EnsureCount(_labels, count, CreateLabel, LabelZIndex);
        for (int i = 0; i < count; i++)
        {
            FibLevel level = _enabledLevels[i];
            Brush brush = GetFrozenBrush(FromRatio(level.Ratio));
            Line line = _lines[i];
            line.Stroke = brush;
            line.StrokeThickness = level.Ratio is 0 or 1 ? 1.6 : 1.1;
            line.Visibility = Visibility.Visible;

            TextBlock label = _labels[i];
            label.Text = level.Label;
            label.Foreground = brush;
            label.Visibility = Visibility.Visible;
        }
    }

    private void RebuildBands(in RulerRenderState state)
    {
        byte alpha = (byte)Math.Round(Math.Clamp(state.BandOpacity, 0, 1) * byte.MaxValue);
        _activeBands.Clear();
        int bandCount = 0;
        foreach ((double lower, double upper) in BandRanges)
        {
            if (!IsEnabledRatio(in state, lower) || !IsEnabledRatio(in state, upper))
            {
                continue;
            }

            Rectangle rect;
            if (bandCount < _bandRects.Count)
            {
                rect = _bandRects[bandCount];
            }
            else
            {
                rect = new Rectangle
                {
                    IsHitTestVisible = false
                };
                Canvas.SetZIndex(rect, BandZIndex);
                _root.Children.Add(rect);
                _bandRects.Add(rect);
            }

            Color color = FromRatio(upper);
            rect.Fill = GetFrozenBrush(Color.FromArgb(alpha, color.R, color.G, color.B));
            rect.Visibility = Visibility.Visible;
            _activeBands.Add((rect, lower, upper));
            bandCount++;
        }

        for (int i = bandCount; i < _bandRects.Count; i++)
        {
            _bandRects[i].Visibility = Visibility.Collapsed;
        }
    }

    private void UpdateGeometry(in RulerRenderState state)
    {
        (double leftX, double rightX) = GetRulerHorizontalBounds(in state);
        double rulerWidth = rightX - leftX;
        double labelX = Math.Max(ScreenMargin, leftX - LabelWidth - LabelMargin);

        for (int i = 0; i < _enabledLevels.Count; i++)
        {
            double y = FibCalculator.CalculateY(state.PointAY, state.PointBY, _enabledLevels[i].Ratio);
            Line line = _lines[i];
            line.X1 = leftX;
            line.Y1 = y;
            line.X2 = rightX;
            line.Y2 = y;
            Canvas.SetLeft(_labels[i], labelX);
            Canvas.SetTop(_labels[i], y - 9);
        }

        foreach ((Rectangle rect, double lower, double upper) in _activeBands)
        {
            double firstY = FibCalculator.CalculateY(state.PointAY, state.PointBY, lower);
            double secondY = FibCalculator.CalculateY(state.PointAY, state.PointBY, upper);
            rect.Width = rulerWidth;
            rect.Height = Math.Abs(secondY - firstY);
            Canvas.SetLeft(rect, leftX);
            Canvas.SetTop(rect, Math.Min(firstY, secondY));
        }

        _anchorLine.X1 = state.PointAX;
        _anchorLine.Y1 = state.PointAY;
        _anchorLine.X2 = state.PointBX;
        _anchorLine.Y2 = state.PointBY;

        _hitLine.X1 = state.PointAX;
        _hitLine.Y1 = state.PointAY;
        _hitLine.X2 = state.PointBX;
        _hitLine.Y2 = state.PointBY;

        SetHandlePosition(_handleA, state.PointAX, state.PointAY);
        SetHandlePosition(_handleB, state.PointBX, state.PointBY);

        if (_deleteButton.Visibility == Visibility.Visible)
        {
            Canvas.SetLeft(_deleteButton, Math.Max(ScreenMargin, rightX - DeleteButtonSize));
            Canvas.SetTop(_deleteButton, ScreenMargin);
        }
    }

    private (double LeftX, double RightX) GetRulerHorizontalBounds(in RulerRenderState state)
    {
        double leftX = Math.Min(state.PointAX, state.PointBX);
        double rightX = Math.Max(state.PointAX, state.PointBX);
        if (rightX - leftX >= 1)
        {
            return (leftX, rightX);
        }

        const double fallbackWidth = 20;
        double centerX = (state.PointAX + state.PointBX) / 2;
        if (_canvas.ActualWidth >= fallbackWidth)
        {
            centerX = Math.Clamp(centerX, fallbackWidth / 2, _canvas.ActualWidth - fallbackWidth / 2);
        }

        return (centerX - fallbackWidth / 2, centerX + fallbackWidth / 2);
    }

    private Color FromRatio(double ratio) => ratio switch
    {
        0 or 1 => _palette["Base"],
        0.236 or 2.000 or 2.618 => _palette["Red"],
        0.382 or 0.618 or 1.272 => _palette["Green"],
        0.500 => _palette["Orange"],
        0.786 or 1.414 => _palette["Cyan"],
        1.618 => _palette["Blue"],
        3.618 => _palette["Purple"],
        4.236 => _palette["Pink"],
        _ when ratio < 0 => RulerEdgeColor,
        _ => _palette["Green"]
    };

    private static bool IsEnabledRatio(in RulerRenderState state, double ratio) =>
        state.Levels.Any(level => Math.Abs(level.Ratio - ratio) <= 0.0001 && level.Enabled);

    private void EnsureCount<T>(List<T> pool, int count, Func<T> factory, int zIndex)
        where T : FrameworkElement
    {
        while (pool.Count < count)
        {
            T element = factory();
            Canvas.SetZIndex(element, zIndex);
            _root.Children.Add(element);
            pool.Add(element);
        }

        for (int i = count; i < pool.Count; i++)
        {
            pool[i].Visibility = Visibility.Collapsed;
        }
    }

    private static Line CreateLevelLine() => new()
    {
        SnapsToDevicePixels = true,
        IsHitTestVisible = false
    };

    private static TextBlock CreateLabel() => new()
    {
        FontSize = 12,
        FontWeight = FontWeights.SemiBold,
        Width = LabelWidth,
        TextAlignment = TextAlignment.Right,
        Padding = new Thickness(0, 1, 0, 1),
        IsHitTestVisible = false
    };

    private Ellipse CreateHandle(Brush brush, DragTarget target) => new()
    {
        Width = HandleDiameter,
        Height = HandleDiameter,
        Fill = Brushes.Transparent,
        Stroke = brush,
        StrokeThickness = 2,
        Tag = target,
        Cursor = Cursors.SizeAll
    };

    private static void SetHandlePosition(Ellipse handle, double x, double y)
    {
        Canvas.SetLeft(handle, x - HandleDiameter / 2);
        Canvas.SetTop(handle, y - HandleDiameter / 2);
    }

    private Button CreateDeleteButton()
    {
        Button button = new()
        {
            Content = "×",
            Width = DeleteButtonSize,
            Height = DeleteButtonSize,
            Padding = new Thickness(0),
            FontSize = 16,
            FontWeight = FontWeights.SemiBold,
            Foreground = Brushes.White,
            Background = GetFrozenBrush(Color.FromArgb(115, 30, 35, 45)),
            BorderBrush = GetFrozenBrush(Color.FromArgb(150, 180, 210, 235)),
            BorderThickness = new Thickness(1),
            Cursor = Cursors.Hand,
            ToolTip = "Delete Fib ruler",
            Visibility = Visibility.Collapsed
        };
        button.Click += (_, eventArgs) =>
        {
            DeleteRequested?.Invoke(this, EventArgs.Empty);
            eventArgs.Handled = true;
        };
        return button;
    }

    private Brush GetFrozenBrush(Color color)
    {
        if (!_brushCache.TryGetValue(color, out Brush? brush))
        {
            brush = new SolidColorBrush(color);
            brush.Freeze();
            _brushCache[color] = brush;
        }

        return brush;
    }

    private static Color ParseColor(string value) =>
        ColorSettings.TryNormalizeHex(value, out string normalized)
            ? (Color)ColorConverter.ConvertFromString(normalized)
            : Colors.Gray;
}
