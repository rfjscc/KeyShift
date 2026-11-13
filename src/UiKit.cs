using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Shapes;

namespace KeyShift
{
    internal static class UiKit
    {
        public static readonly FontFamily BaseFont =
            new FontFamily("Segoe UI, Microsoft YaHei UI");

        public static readonly FontFamily MonoFont =
            new FontFamily("Consolas, Microsoft YaHei UI");

        public static readonly Color BackgroundTop = Hex("#090E18");
        public static readonly Color BackgroundBottom = Hex("#101827");
        public static readonly Color CardTop = Hex("#161F2F");
        public static readonly Color CardBottom = Hex("#111825");
        public static readonly Color CardBorder = Hex("#26344A");
        public static readonly Color InnerCard = Hex("#0E1725");
        public static readonly Color InnerBorder = Hex("#253249");

        public static readonly Brush TextPrimary = Brush("#F3F6FC");
        public static readonly Brush TextSecondary = Brush("#A0ADC1");
        public static readonly Brush TextMuted = Brush("#6F7D93");
        public static readonly Brush TextFaint = Brush("#526076");
        public static readonly Brush Green = Brush("#57E0B7");
        public static readonly Brush Amber = Brush("#FFC46B");
        public static readonly Brush Red = Brush("#FF7485");
        public static readonly Brush Blue = Brush("#7E9BFF");

        public static readonly Color UsAccent = Hex("#7E9BFF");
        public static readonly Color UkAccent = Hex("#FF8494");
        public static readonly Color UnknownAccent = Hex("#93A1B7");

        private static ControlTemplate _primaryButtonTemplate;
        private static ControlTemplate _textButtonTemplate;
        private static ControlTemplate _windowButtonTemplate;
        private static ControlTemplate _closeButtonTemplate;

        public static TextBlock Label(
            string text,
            double size,
            Brush foreground,
            FontWeight weight)
        {
            TextBlock block = new TextBlock();
            block.Text = text;
            block.FontFamily = BaseFont;
            block.FontSize = size;
            block.Foreground = foreground;
            block.FontWeight = weight;
            block.TextWrapping = TextWrapping.NoWrap;
            TextOptions.SetTextFormattingMode(block, TextFormattingMode.Display);
            TextOptions.SetTextRenderingMode(block, TextRenderingMode.ClearType);
            return block;
        }

        public static TextBlock Label(string text, double size, Brush foreground)
        {
            return Label(text, size, foreground, FontWeights.Normal);
        }

        public static TextBlock MonoLabel(string text, double size, Brush foreground)
        {
            TextBlock block = Label(text, size, foreground);
            block.FontFamily = MonoFont;
            block.FontWeight = FontWeights.Normal;
            return block;
        }

        public static Border Divider()
        {
            return new Border
            {
                Height = 1,
                Background = Brush("#12FFFFFF"),
                HorizontalAlignment = HorizontalAlignment.Stretch
            };
        }

        public static Border RoundedBorder(
            UIElement child,
            Brush background,
            Brush border,
            double radius,
            Thickness padding)
        {
            Border borderElement = new Border();
            borderElement.Background = background;
            borderElement.BorderBrush = border;
            borderElement.BorderThickness = new Thickness(1);
            borderElement.CornerRadius = new CornerRadius(radius);
            borderElement.Padding = padding;
            borderElement.SnapsToDevicePixels = true;
            borderElement.Child = child;
            return borderElement;
        }

        public static Border Card()
        {
            LinearGradientBrush background = new LinearGradientBrush(
                CardTop, CardBottom, new Point(0, 0), new Point(0.6, 1));
            background.Freeze();

            Border card = new Border();
            card.Background = background;
            card.BorderBrush = Brush(CardBorder);
            card.BorderThickness = new Thickness(1);
            card.CornerRadius = new CornerRadius(18);
            card.SnapsToDevicePixels = true;
            return card;
        }

        public static LinearGradientBrush Gradient(Color start, Color end)
        {
            LinearGradientBrush brush = new LinearGradientBrush(
                start, end, new Point(0, 0), new Point(1, 1));
            brush.Freeze();
            return brush;
        }

        public static Color AccentFor(LayoutKind kind)
        {
            if (kind == LayoutKind.Uk) return UkAccent;
            if (kind == LayoutKind.Us) return UsAccent;
            return UnknownAccent;
        }

        public static Brush AccentBrushFor(LayoutKind kind)
        {
            return Brush(AccentFor(kind));
        }

        public static LinearGradientBrush AccentGradientFor(LayoutKind kind)
        {
            if (kind == LayoutKind.Uk)
            {
                return Gradient(Hex("#FF7D8E"), Hex("#EF5A76"));
            }

            if (kind == LayoutKind.Us)
            {
                return Gradient(Hex("#7092FF"), Hex("#6C63FF"));
            }

            return Gradient(Hex("#8593A8"), Hex("#68758A"));
        }

        public static SolidColorBrush Tint(Color color, byte alpha)
        {
            return Brush(Color.FromArgb(alpha, color.R, color.G, color.B));
        }

        public static ControlTemplate PrimaryButtonTemplate
        {
            get
            {
                if (_primaryButtonTemplate == null)
                {
                    const string xaml = @"
<ControlTemplate xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation'
                 xmlns:x='http://schemas.microsoft.com/winfx/2006/xaml'
                 TargetType='{x:Type Button}'>
  <Grid x:Name='Root'>
    <Border x:Name='BackgroundBorder'
            CornerRadius='14'
            Background='{TemplateBinding Background}'
            SnapsToDevicePixels='True' />
    <Border x:Name='Hover'
            CornerRadius='14'
            Background='White'
            Opacity='0'
            IsHitTestVisible='False' />
    <Border x:Name='FocusBorder'
            CornerRadius='14'
            BorderBrush='White'
            BorderThickness='1.5'
            Opacity='0'
            Margin='2'
            IsHitTestVisible='False' />
    <ContentPresenter Margin='{TemplateBinding Padding}'
                      HorizontalAlignment='Stretch'
                      VerticalAlignment='Center'
                      SnapsToDevicePixels='True' />
  </Grid>
  <ControlTemplate.Triggers>
    <Trigger Property='IsMouseOver' Value='True'>
      <Setter TargetName='Hover' Property='Opacity' Value='0.075' />
    </Trigger>
    <Trigger Property='IsPressed' Value='True'>
      <Setter TargetName='Hover' Property='Opacity' Value='0.15' />
    </Trigger>
    <Trigger Property='IsKeyboardFocused' Value='True'>
      <Setter TargetName='FocusBorder' Property='Opacity' Value='0.8' />
    </Trigger>
    <Trigger Property='IsEnabled' Value='False'>
      <Setter TargetName='Root' Property='Opacity' Value='0.48' />
    </Trigger>
  </ControlTemplate.Triggers>
</ControlTemplate>";
                    _primaryButtonTemplate = (ControlTemplate)XamlReader.Parse(xaml);
                }

                return _primaryButtonTemplate;
            }
        }

        public static ControlTemplate TextButtonTemplate
        {
            get
            {
                if (_textButtonTemplate == null)
                {
                    const string xaml = @"
<ControlTemplate xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation'
                 xmlns:x='http://schemas.microsoft.com/winfx/2006/xaml'
                 TargetType='{x:Type Button}'>
  <Border x:Name='Root'
          CornerRadius='8'
          Background='Transparent'
          SnapsToDevicePixels='True'>
    <ContentPresenter Margin='{TemplateBinding Padding}'
                      HorizontalAlignment='{TemplateBinding HorizontalContentAlignment}'
                      VerticalAlignment='Center' />
  </Border>
  <ControlTemplate.Triggers>
    <Trigger Property='IsMouseOver' Value='True'>
      <Setter TargetName='Root' Property='Background' Value='#10FFFFFF' />
    </Trigger>
    <Trigger Property='IsPressed' Value='True'>
      <Setter TargetName='Root' Property='Background' Value='#18FFFFFF' />
    </Trigger>
    <Trigger Property='IsKeyboardFocused' Value='True'>
      <Setter TargetName='Root' Property='Background' Value='#12FFFFFF' />
    </Trigger>
    <Trigger Property='IsEnabled' Value='False'>
      <Setter TargetName='Root' Property='Opacity' Value='0.42' />
    </Trigger>
  </ControlTemplate.Triggers>
</ControlTemplate>";
                    _textButtonTemplate = (ControlTemplate)XamlReader.Parse(xaml);
                }

                return _textButtonTemplate;
            }
        }

        public static ControlTemplate WindowButtonTemplate
        {
            get
            {
                if (_windowButtonTemplate == null)
                {
                    _windowButtonTemplate = ParseWindowButtonTemplate("#18FFFFFF");
                }

                return _windowButtonTemplate;
            }
        }

        public static ControlTemplate CloseButtonTemplate
        {
            get
            {
                if (_closeButtonTemplate == null)
                {
                    _closeButtonTemplate = ParseWindowButtonTemplate("#3AFF7185");
                }

                return _closeButtonTemplate;
            }
        }

        private static ControlTemplate ParseWindowButtonTemplate(string hoverBackground)
        {
            string xaml = @"
<ControlTemplate xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation'
                 xmlns:x='http://schemas.microsoft.com/winfx/2006/xaml'
                 TargetType='{x:Type Button}'>
  <Grid>
    <Border x:Name='Root'
            CornerRadius='8'
            Background='Transparent'
            SnapsToDevicePixels='True' />
    <ContentPresenter x:Name='Presenter'
                      HorizontalAlignment='Center'
                      VerticalAlignment='Center' />
  </Grid>
  <ControlTemplate.Triggers>
    <Trigger Property='IsMouseOver' Value='True'>
      <Setter TargetName='Root' Property='Background' Value='__HOVER_BG__' />
      <Setter TargetName='Presenter' Property='Opacity' Value='1' />
    </Trigger>
    <Trigger Property='IsPressed' Value='True'>
      <Setter TargetName='Root' Property='Opacity' Value='0.82' />
    </Trigger>
    <Trigger Property='IsKeyboardFocused' Value='True'>
      <Setter TargetName='Root' Property='Background' Value='#14FFFFFF' />
    </Trigger>
  </ControlTemplate.Triggers>
</ControlTemplate>";
            xaml = xaml.Replace("__HOVER_BG__", hoverBackground);
            return (ControlTemplate)XamlReader.Parse(xaml);
        }

        public static Border Logo()
        {
            Border tile = new Border();
            tile.Width = 34;
            tile.Height = 34;
            tile.CornerRadius = new CornerRadius(10);
            tile.Background = Gradient(Hex("#6F8BFF"), Hex("#9A6BFF"));
            tile.SnapsToDevicePixels = true;

            Canvas canvas = new Canvas();
            canvas.Width = 20;
            canvas.Height = 16;
            canvas.HorizontalAlignment = HorizontalAlignment.Center;
            canvas.VerticalAlignment = VerticalAlignment.Center;

            double[] xs = new double[] { 2.6, 7.9, 13.2 };
            for (int i = 0; i < xs.Length; i++)
            {
                AddKey(canvas, xs[i], 2.0, 4.2, 3.0, i == 1 ? 0.72 : 0.96);
                AddKey(canvas, xs[i], 6.3, 4.2, 3.0, 0.96);
            }

            AddKey(canvas, 2.6, 10.6, 14.8, 2.7, 0.96);
            tile.Child = canvas;
            return tile;
        }

        private static void AddKey(
            Canvas canvas, double x, double y, double width, double height, double opacity)
        {
            Border key = new Border();
            key.Width = width;
            key.Height = height;
            key.CornerRadius = new CornerRadius(1.1);
            key.Background = new SolidColorBrush(Color.FromArgb(
                (byte)Math.Round(255 * opacity), 255, 255, 255));
            Canvas.SetLeft(key, x);
            Canvas.SetTop(key, y);
            canvas.Children.Add(key);
        }

        public static FrameworkElement ShieldIcon(Brush stroke, double size)
        {
            double factor = size / 14.0;
            Canvas canvas = new Canvas();
            canvas.Width = size;
            canvas.Height = size * 16.0 / 14.0;

            Path shield = new Path();
            shield.Data = Geometry.Parse(
                "M 7,0.8 L 13,3.2 L 13,7.2 C 13,10.8 10.5,13.6 7,14.9 " +
                "C 3.5,13.6 1,10.8 1,7.2 L 1,3.2 Z");
            shield.Stroke = stroke;
            shield.StrokeThickness = 1.15 * factor;
            shield.StrokeLineJoin = PenLineJoin.Round;
            shield.Fill = Brushes.Transparent;
            shield.Stretch = Stretch.None;
            shield.RenderTransform = new ScaleTransform(factor, factor);
            canvas.Children.Add(shield);
            return canvas;
        }

        public static FrameworkElement ClockIcon(Brush stroke, double size)
        {
            Canvas canvas = new Canvas();
            canvas.Width = size;
            canvas.Height = size;

            Ellipse ring = new Ellipse();
            ring.Width = size - 1.5;
            ring.Height = size - 1.5;
            ring.Stroke = stroke;
            ring.StrokeThickness = 1.1;
            Canvas.SetLeft(ring, 0.75);
            Canvas.SetTop(ring, 0.75);
            canvas.Children.Add(ring);

            Line hand = new Line();
            hand.X1 = size * 0.52;
            hand.Y1 = size * 0.27;
            hand.X2 = size * 0.52;
            hand.Y2 = size * 0.56;
            hand.Stroke = stroke;
            hand.StrokeThickness = 1.1;
            hand.StrokeStartLineCap = PenLineCap.Round;
            hand.StrokeEndLineCap = PenLineCap.Round;
            canvas.Children.Add(hand);

            Line tick = new Line();
            tick.X1 = size * 0.52;
            tick.Y1 = size * 0.56;
            tick.X2 = size * 0.72;
            tick.Y2 = size * 0.66;
            tick.Stroke = stroke;
            tick.StrokeThickness = 1.1;
            tick.StrokeStartLineCap = PenLineCap.Round;
            tick.StrokeEndLineCap = PenLineCap.Round;
            canvas.Children.Add(tick);
            return canvas;
        }

        public static FrameworkElement ArrowIcon(Brush stroke, double size)
        {
            Canvas canvas = new Canvas();
            canvas.Width = size;
            canvas.Height = size;

            Path arrow = new Path();
            arrow.Data = Geometry.Parse("M 2,7 L 12,7 M 8,3 L 12,7 L 8,11");
            arrow.Stroke = stroke;
            arrow.StrokeThickness = 1.5;
            arrow.StrokeStartLineCap = PenLineCap.Round;
            arrow.StrokeEndLineCap = PenLineCap.Round;
            arrow.StrokeLineJoin = PenLineJoin.Round;
            arrow.Stretch = Stretch.None;
            arrow.RenderTransform = new ScaleTransform(size / 14.0, size / 14.0);
            canvas.Children.Add(arrow);
            return canvas;
        }

        public static FrameworkElement WindowGlyph(bool close, Brush stroke)
        {
            Canvas canvas = new Canvas();
            canvas.Width = 12;
            canvas.Height = 12;

            Path path = new Path();
            path.Data = close
                ? Geometry.Parse("M 2.5,2.5 L 9.5,9.5 M 9.5,2.5 L 2.5,9.5")
                : Geometry.Parse("M 2.5,6 L 9.5,6");
            path.Stroke = stroke;
            path.StrokeThickness = 1.15;
            path.StrokeStartLineCap = PenLineCap.Round;
            path.StrokeEndLineCap = PenLineCap.Round;
            canvas.Children.Add(path);
            return canvas;
        }

        public static DropShadowEffect Shadow(double opacity, double blur, double depth)
        {
            DropShadowEffect effect = new DropShadowEffect();
            effect.Color = Colors.Black;
            effect.Opacity = opacity;
            effect.BlurRadius = blur;
            effect.ShadowDepth = depth;
            effect.Direction = 270;
            effect.Freeze();
            return effect;
        }

        public static Color Hex(string value)
        {
            return (Color)ColorConverter.ConvertFromString(value);
        }

        public static SolidColorBrush Brush(string value)
        {
            return Brush(Hex(value));
        }

        public static SolidColorBrush Brush(Color color)
        {
            SolidColorBrush brush = new SolidColorBrush(color);
            brush.Freeze();
            return brush;
        }
    }
}
