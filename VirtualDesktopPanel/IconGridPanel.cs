using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;

// Disambiguate types that also exist in System.Windows.Forms (included via global usings)
using Panel = System.Windows.Controls.Panel;
using Point = System.Windows.Point;
using Size = System.Windows.Size;
using MouseEventArgs = System.Windows.Input.MouseEventArgs;
using WpfColor = System.Windows.Media.Color;
using WpfBrushes = System.Windows.Media.Brushes;
using WpfColorConverter = System.Windows.Media.ColorConverter;
using WpfMessageBox = System.Windows.MessageBox;
using WpfHorizontalAlignment = System.Windows.HorizontalAlignment;

namespace VirtualDesktopPanel;

public class IconGridPanel : Panel
{
    public int CellWidth { get; set; } = 80;
    public int CellHeight { get; set; } = 100;
    public int HorizontalSpacing { get; set; } = 12;
    public int VerticalSpacing { get; set; } = 16;
    public int PaddingLeft { get; set; } = 24;
    public int PaddingTop { get; set; } = 24;

    public event Action<DesktopIcon, int, int>? IconMoved; // icon, newRow, newCol
    public event Action<DesktopIcon>? IconDoubleClicked;

    private readonly Dictionary<UIElement, DesktopIcon> _iconMap = new();
    private UIElement? _draggingElement;
    private Point _dragStartPoint;
    private bool _isDragging;

    protected override Size MeasureOverride(Size availableSize)
    {
        foreach (UIElement child in Children)
        {
            child.Measure(new Size(CellWidth, CellHeight));
        }
        return availableSize;
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var sorted = new List<(DesktopIcon icon, UIElement element)>();
        foreach (UIElement child in Children)
        {
            if (_iconMap.TryGetValue(child, out var icon))
                sorted.Add((icon, child));
        }

        sorted.Sort((a, b) =>
        {
            int cmp = a.icon.Row.CompareTo(b.icon.Row);
            return cmp != 0 ? cmp : a.icon.Col.CompareTo(b.icon.Col);
        });

        foreach (var (icon, child) in sorted)
        {
            if (child == _draggingElement) continue;

            double x = PaddingLeft + icon.Col * (CellWidth + HorizontalSpacing);
            double y = PaddingTop + icon.Row * (CellHeight + VerticalSpacing);
            child.Arrange(new Rect(x, y, CellWidth, CellHeight));
        }

        return finalSize;
    }

    public void Populate(List<DesktopIcon> icons)
    {
        Children.Clear();
        _iconMap.Clear();

        foreach (var icon in icons)
        {
            var item = CreateIconElement(icon);
            Children.Add(item);
            _iconMap[item] = icon;
        }
    }

    public void AddIcon(DesktopIcon icon)
    {
        var item = CreateIconElement(icon);
        Children.Add(item);
        _iconMap[item] = icon;

        var animation = new DoubleAnimation(1.0, 0.3, TimeSpan.FromMilliseconds(200))
        {
            AutoReverse = true,
            RepeatBehavior = new RepeatBehavior(2)
        };
        item.BeginAnimation(OpacityProperty, animation);
    }

    public void RemoveIcon(string filePath)
    {
        UIElement? toRemove = null;
        foreach (var kv in _iconMap)
        {
            if (kv.Value.FilePath == filePath)
            {
                toRemove = kv.Key;
                break;
            }
        }
        if (toRemove != null)
        {
            Children.Remove(toRemove);
            _iconMap.Remove(toRemove);
        }
    }

    public void UpdateLabel(string oldPath, string newPath)
    {
        foreach (var kv in _iconMap)
        {
            if (kv.Value.FilePath == oldPath)
            {
                kv.Value.Label = System.IO.Path.GetFileNameWithoutExtension(newPath);
                if (kv.Key is Border border && border.Child is StackPanel sp
                    && sp.Children.Count >= 2 && sp.Children[1] is TextBlock tb)
                {
                    tb.Text = kv.Value.Label;
                }
                break;
            }
        }
    }

    public void UpdateBackground(string colorHex, double opacity)
    {
        var color = (WpfColor)WpfColorConverter.ConvertFromString(colorHex);
        color.A = (byte)(opacity * 255);
        Background = new SolidColorBrush(color);
    }

    private UIElement CreateIconElement(DesktopIcon icon)
    {
        var image = new System.Windows.Controls.Image
        {
            Source = icon.IconImage,
            Width = 40,
            Height = 40,
            Stretch = Stretch.Uniform,
            Margin = new Thickness(0, 0, 0, 4)
        };

        if (icon.IconImage == null)
        {
            image.Source = CreatePlaceholderIcon();
        }

        var label = new TextBlock
        {
            Text = icon.Label,
            TextAlignment = TextAlignment.Center,
            FontSize = 11,
            Foreground = WpfBrushes.White,
            TextWrapping = TextWrapping.WrapWithOverflow,
            MaxWidth = CellWidth - 4,
            MaxHeight = 32,
            TextTrimming = TextTrimming.CharacterEllipsis
        };

        var brokenOverlay = new TextBlock
        {
            Text = "!",
            FontSize = 14,
            FontWeight = FontWeights.Bold,
            Foreground = WpfBrushes.OrangeRed,
            HorizontalAlignment = WpfHorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Top,
            Margin = new Thickness(0, -2, 2, 0),
            Visibility = icon.IsBroken ? Visibility.Visible : Visibility.Collapsed
        };

        var imageContainer = new Grid { Width = 40, Height = 40 };
        imageContainer.Children.Add(image);
        imageContainer.Children.Add(brokenOverlay);

        var stack = new StackPanel
        {
            Width = CellWidth,
            HorizontalAlignment = WpfHorizontalAlignment.Center
        };
        stack.Children.Add(imageContainer);
        stack.Children.Add(label);

        var border = new Border
        {
            Width = CellWidth,
            Height = CellHeight,
            Child = stack,
            Background = WpfBrushes.Transparent,
            CornerRadius = new CornerRadius(4)
        };

        border.MouseLeftButtonDown += OnMouseLeftButtonDown;
        border.MouseMove += OnMouseMove;
        border.MouseLeftButtonUp += OnMouseLeftButtonUp;
        border.MouseDown += (_, e) =>
        {
            if (!_isDragging && e.ClickCount == 2)
            {
                if (icon.IsBroken)
                {
                    WpfMessageBox.Show($"快捷方式目标不存在:\n{icon.FilePath}",
                        "无法打开", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
                else
                {
                    IconDoubleClicked?.Invoke(icon);
                }
            }
        };

        border.ContextMenu = CreateContextMenu(icon);

        border.MouseEnter += (_, _) =>
        {
            border.Background = new SolidColorBrush(WpfColor.FromArgb(40, 255, 255, 255));
        };
        border.MouseLeave += (_, _) =>
        {
            border.Background = WpfBrushes.Transparent;
        };

        return border;
    }

    private static ImageSource CreatePlaceholderIcon()
    {
        var size = 40;
        var visual = new DrawingVisual();
        using (var ctx = visual.RenderOpen())
        {
            ctx.DrawRectangle(new SolidColorBrush(WpfColor.FromArgb(80, 128, 128, 128)),
                null, new Rect(4, 4, size - 8, size - 8));
        }
        var renderTarget = new RenderTargetBitmap(size, size, 96, 96, PixelFormats.Pbgra32);
        renderTarget.Render(visual);
        return renderTarget;
    }

    private ContextMenu CreateContextMenu(DesktopIcon icon)
    {
        var menu = new ContextMenu();

        var open = new MenuItem { Header = "打开" };
        open.Click += (_, _) => icon.Launch();

        var openLocation = new MenuItem { Header = "打开文件位置" };
        openLocation.Click += (_, _) =>
        {
            var dir = System.IO.Path.GetDirectoryName(icon.FilePath);
            if (dir != null)
                System.Diagnostics.Process.Start("explorer.exe", $"/select,\"{icon.FilePath}\"");
        };

        var delete = new MenuItem { Header = "删除" };
        delete.Click += (_, _) =>
        {
            var result = WpfMessageBox.Show(
                $"确定要将 \"{icon.Label}\" 移动到回收站吗？",
                "删除文件", MessageBoxButton.YesNo, MessageBoxImage.Warning);
            if (result == MessageBoxResult.Yes)
            {
                try
                {
                    var fileOp = new NativeMethods.SHFILEOPSTRUCT
                    {
                        hwnd = IntPtr.Zero,
                        wFunc = NativeMethods.FO_DELETE,
                        pFrom = icon.FilePath + '\0',
                        fFlags = NativeMethods.FOF_ALLOWUNDO
                    };
                    NativeMethods.SHFileOperation(ref fileOp);
                }
                catch (Exception ex)
                {
                    WpfMessageBox.Show($"删除失败:\n{ex.Message}", "错误",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        };

        var properties = new MenuItem { Header = "属性" };
        properties.Click += (_, _) =>
        {
            System.Diagnostics.Process.Start("explorer.exe", $"/select,\"{icon.FilePath}\"");
        };

        menu.Items.Add(open);
        menu.Items.Add(new Separator());
        menu.Items.Add(openLocation);
        menu.Items.Add(delete);
        menu.Items.Add(new Separator());
        menu.Items.Add(properties);

        return menu;
    }

    private void OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is UIElement element && _iconMap.ContainsKey(element))
        {
            _draggingElement = element;
            _dragStartPoint = e.GetPosition(this);
            _isDragging = false;
            element.CaptureMouse();
        }
    }

    private void OnMouseMove(object sender, MouseEventArgs e)
    {
        if (_draggingElement == null) return;

        var currentPos = e.GetPosition(this);
        var delta = currentPos - _dragStartPoint;

        if (!_isDragging && Math.Abs(delta.X) < 5 && Math.Abs(delta.Y) < 5)
            return;

        _isDragging = true;

        _draggingElement.RenderTransform = new ScaleTransform(1.08, 1.08);
        _draggingElement.Opacity = 0.8;

        InvalidateArrange();
    }

    private void OnMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (_draggingElement == null) return;
        _draggingElement.ReleaseMouseCapture();

        if (_isDragging)
        {
            var currentPos = e.GetPosition(this);

            int newCol = (int)Math.Round((currentPos.X - PaddingLeft) / (double)(CellWidth + HorizontalSpacing));
            int newRow = (int)Math.Round((currentPos.Y - PaddingTop) / (double)(CellHeight + VerticalSpacing));
            newCol = Math.Max(0, newCol);
            newRow = Math.Max(0, newRow);

            if (_iconMap.TryGetValue(_draggingElement, out var icon))
            {
                int oldRow = icon.Row, oldCol = icon.Col;
                icon.Row = newRow;
                icon.Col = newCol;

                foreach (var kv in _iconMap)
                {
                    if (kv.Key != _draggingElement
                        && kv.Value.Row == newRow && kv.Value.Col == newCol)
                    {
                        kv.Value.Row = oldRow;
                        kv.Value.Col = oldCol;
                        break;
                    }
                }

                IconMoved?.Invoke(icon, newRow, newCol);
            }

            _draggingElement.RenderTransform = Transform.Identity;
            _draggingElement.Opacity = 1.0;
        }

        _draggingElement = null;
        _isDragging = false;
        InvalidateArrange();
    }

    public int GetFirstEmptySlot(out int row, out int col)
    {
        var occupied = new HashSet<(int, int)>();
        foreach (var icon in _iconMap.Values)
            occupied.Add((icon.Row, icon.Col));

        row = 0; col = 0;
        while (true)
        {
            if (!occupied.Contains((row, col))) return 0;
            col++;
            if (col >= 20) { col = 0; row++; }
        }
    }

    private TextBlock? _emptyMessage;

    public void ShowEmptyMessage()
    {
        if (_emptyMessage == null)
        {
            _emptyMessage = new TextBlock
            {
                Text = "桌面暂无文件\n\n将文件、快捷方式添加到桌面文件夹后\n将自动显示在此处",
                FontSize = 14,
                Foreground = new SolidColorBrush(WpfColor.FromRgb(128, 128, 128)),
                TextAlignment = TextAlignment.Center,
                TextWrapping = TextWrapping.Wrap,
                HorizontalAlignment = WpfHorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            Children.Add(_emptyMessage);
        }
        _emptyMessage.Visibility = Visibility.Visible;
    }

    public void HideEmptyMessage()
    {
        if (_emptyMessage != null)
            _emptyMessage.Visibility = Visibility.Collapsed;
    }
}
