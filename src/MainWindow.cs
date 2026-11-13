using System;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Shell;
using System.Windows.Threading;
using Ellipse = System.Windows.Shapes.Ellipse;

namespace KeyShift
{
    internal enum ToastKind
    {
        Info,
        Success,
        Error
    }

    internal sealed class MainWindow : Window
    {
        private const double DesignWidth = 900.0;
        private const double DesignHeight = 540.0;
        private const string DefaultResetText = "恢复系统默认";

        private readonly CommandOptions _options;

        private LayoutState _state;
        private bool _isElevated;
        private bool _pendingRestart;
        private bool _resetArmed;
        private string _startupMessage;
        private ToastKind _startupToastKind;
        private string _initialReadError;

        private Grid _designRoot;
        private TranslateTransform _rootSlide;

        private TextBlock _layoutName;
        private TextBlock _layoutCountry;
        private TextBlock _layoutFile;
        private Border _layoutBadge;
        private TextBlock _layoutBadgeText;
        private Border _statusPill;
        private Ellipse _statusDot;
        private TextBlock _statusText;
        private Grid _keysHost;
        private TextBlock _keyCaption;

        private Border _targetBadge;
        private TextBlock _targetBadgeText;
        private TextBlock _targetName;
        private TextBlock _targetMeta;
        private Button _applyButton;
        private TextBlock _applyLabel;
        private TextBlock _permissionHint;
        private Button _resetButton;
        private TextBlock _resetLabel;
        private TextBlock _headerPermissionText;
        private Ellipse _headerPermissionDot;
        private Border _headerPermissionPill;

        private Border _toast;
        private TextBlock _toastText;
        private TextBlock _toastGlyph;
        private Border _toastGlyphBubble;
        private TranslateTransform _toastSlide;
        private DispatcherTimer _toastTimer;
        private DispatcherTimer _resetTimer;
        private int _toastGeneration;

#if PREVIEW
        private DispatcherTimer _screenshotTimer;
#endif

        public MainWindow(CommandOptions options)
        {
            _options = options ?? new CommandOptions();

            _isElevated = ElevationHelper.IsElevated;
#if PREVIEW
            if (_options.ApplyTarget.HasValue)
            {
                _isElevated = true;
            }
#endif

            _toastTimer = new DispatcherTimer();
            _toastTimer.Tick += OnToastTimerTick;

            _resetTimer = new DispatcherTimer();
            _resetTimer.Interval = TimeSpan.FromSeconds(4);
            _resetTimer.Tick += OnResetTimerTick;

            _state = SafeReadState();

            if (_options.ApplyTarget.HasValue)
            {
                ApplyStartupTarget(_options.ApplyTarget.Value);
            }
            else if (!string.IsNullOrEmpty(_initialReadError))
            {
                _startupMessage = "无法读取当前布局：" + _initialReadError;
                _startupToastKind = ToastKind.Error;
            }

            ConfigureWindow();
            _designRoot = BuildInterface();
            Content = WrapInterface(_designRoot);
            RefreshUi();

            Loaded += OnLoaded;
            PreviewKeyDown += OnPreviewKeyDown;
            Closed += OnWindowClosed;
        }

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);

            try
            {
                IntPtr handle = new WindowInteropHelper(this).Handle;
                int darkMode = 1;
                DwmSetWindowAttribute(handle, 20, ref darkMode, 4);

                int roundCorners = 2;
                DwmSetWindowAttribute(handle, 33, ref roundCorners, 4);
            }
            catch
            {
                // The DWM attributes are cosmetic and are not available on every Windows build.
            }
        }

        [DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(
            IntPtr windowHandle, int attribute, ref int attributeValue, int attributeSize);

        private void ConfigureWindow()
        {
            Title = "KeyShift · 键盘布局切换器";
            WindowStyle = WindowStyle.None;
            ResizeMode = ResizeMode.NoResize;
            AllowsTransparency = false;
            Background = UiKit.Brush(UiKit.BackgroundTop);
            UseLayoutRounding = true;
            SnapsToDevicePixels = true;
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            ShowInTaskbar = true;

            WindowChrome chrome = new WindowChrome();
            chrome.CaptionHeight = 0;
            chrome.ResizeBorderThickness = new Thickness(0);
            chrome.GlassFrameThickness = new Thickness(0);
            chrome.CornerRadius = new CornerRadius(0);
            chrome.UseAeroCaptionButtons = false;
            WindowChrome.SetWindowChrome(this, chrome);

            double maxWidth = SystemParameters.WorkArea.Width - 28;
            double maxHeight = SystemParameters.WorkArea.Height - 28;
            double scale = Math.Min(1.0, Math.Min(maxWidth / DesignWidth, maxHeight / DesignHeight));
            if (scale < 0.55)
            {
                scale = 0.55;
            }

            Width = Math.Round(DesignWidth * scale);
            Height = Math.Round(DesignHeight * scale);

            AddToolTipStyle();
            LoadWindowIcon();
        }

        private void LoadWindowIcon()
        {
            try
            {
                using (Stream stream = Assembly.GetExecutingAssembly()
                    .GetManifestResourceStream("app.ico"))
                {
                    if (stream == null)
                    {
                        return;
                    }

                    IconBitmapDecoder decoder = new IconBitmapDecoder(
                        stream,
                        BitmapCreateOptions.PreservePixelFormat,
                        BitmapCacheOption.OnLoad);

                    if (decoder.Frames.Count > 0)
                    {
                        Icon = decoder.Frames[decoder.Frames.Count - 1];
                    }
                }
            }
            catch
            {
                // The executable's Win32 icon still covers the taskbar and shell.
            }
        }

        private void AddToolTipStyle()
        {
            const string xaml = @"
<Style xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation'
       xmlns:x='http://schemas.microsoft.com/winfx/2006/xaml'
       TargetType='{x:Type ToolTip}'>
  <Setter Property='Background' Value='#1C2739' />
  <Setter Property='BorderBrush' Value='#33435C' />
  <Setter Property='Foreground' Value='#DCE5F2' />
  <Setter Property='FontFamily' Value='Microsoft YaHei UI' />
  <Setter Property='FontSize' Value='10.5' />
  <Setter Property='Padding' Value='10,7' />
  <Setter Property='HasDropShadow' Value='False' />
  <Setter Property='MaxWidth' Value='380' />
  <Setter Property='Template'>
    <Setter.Value>
      <ControlTemplate TargetType='{x:Type ToolTip}'>
        <Border Background='{TemplateBinding Background}'
                BorderBrush='{TemplateBinding BorderBrush}'
                BorderThickness='1'
                CornerRadius='8'
                Padding='{TemplateBinding Padding}'>
          <ContentPresenter />
        </Border>
      </ControlTemplate>
    </Setter.Value>
  </Setter>
</Style>";
            Style style = (Style)XamlReader.Parse(xaml);
            Resources.Add(typeof(ToolTip), style);
        }

        private FrameworkElement WrapInterface(Grid designRoot)
        {
            Grid host = new Grid();
            host.Background = UiKit.Brush(UiKit.BackgroundTop);
            host.UseLayoutRounding = true;

            Viewbox viewbox = new Viewbox();
            viewbox.Stretch = Stretch.Uniform;
            viewbox.StretchDirection = StretchDirection.Both;
            viewbox.HorizontalAlignment = HorizontalAlignment.Stretch;
            viewbox.VerticalAlignment = VerticalAlignment.Stretch;
            viewbox.Child = designRoot;
            host.Children.Add(viewbox);
            return host;
        }

        private Grid BuildInterface()
        {
            Grid root = new Grid();
            root.Width = DesignWidth;
            root.Height = DesignHeight;
            root.ClipToBounds = true;
            root.UseLayoutRounding = true;

            LinearGradientBrush background = new LinearGradientBrush();
            background.StartPoint = new Point(0, 0);
            background.EndPoint = new Point(1, 1);
            background.GradientStops.Add(new GradientStop(UiKit.BackgroundTop, 0));
            background.GradientStops.Add(new GradientStop(UiKit.Hex("#0C1320"), 0.56));
            background.GradientStops.Add(new GradientStop(UiKit.BackgroundBottom, 1));
            background.Freeze();
            root.Background = background;

            Ellipse blueGlow = CreateGlow(570, UiKit.Hex("#345BFF"), 0.52);
            blueGlow.HorizontalAlignment = HorizontalAlignment.Left;
            blueGlow.VerticalAlignment = VerticalAlignment.Top;
            blueGlow.Margin = new Thickness(-220, -280, 0, 0);
            root.Children.Add(blueGlow);

            Ellipse violetGlow = CreateGlow(520, UiKit.Hex("#7B45FF"), 0.4);
            violetGlow.HorizontalAlignment = HorizontalAlignment.Right;
            violetGlow.VerticalAlignment = VerticalAlignment.Bottom;
            violetGlow.Margin = new Thickness(0, 0, -220, -290);
            root.Children.Add(violetGlow);

            Border topHairline = new Border();
            topHairline.Height = 1;
            topHairline.Margin = new Thickness(48, 0, 48, 0);
            topHairline.VerticalAlignment = VerticalAlignment.Top;
            topHairline.IsHitTestVisible = false;
            LinearGradientBrush hairline = new LinearGradientBrush();
            hairline.StartPoint = new Point(0, 0);
            hairline.EndPoint = new Point(1, 0);
            hairline.GradientStops.Add(new GradientStop(Color.FromArgb(0, 130, 160, 255), 0));
            hairline.GradientStops.Add(new GradientStop(Color.FromArgb(110, 130, 160, 255), 0.48));
            hairline.GradientStops.Add(new GradientStop(Color.FromArgb(0, 130, 160, 255), 1));
            hairline.Freeze();
            topHairline.Background = hairline;
            root.Children.Add(topHairline);

            Grid main = new Grid();
            main.RowDefinitions.Add(new RowDefinition { Height = new GridLength(52) });
            main.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            root.Children.Add(main);

            FrameworkElement titleBar = BuildTitleBar();
            Grid.SetRow(titleBar, 0);
            main.Children.Add(titleBar);

            FrameworkElement body = BuildBody();
            Grid.SetRow(body, 1);
            main.Children.Add(body);

            _toast = BuildToast();
            root.Children.Add(_toast);

            return root;
        }

        private static Ellipse CreateGlow(double size, Color color, double opacity)
        {
            RadialGradientBrush glow = new RadialGradientBrush();
            glow.Center = new Point(0.5, 0.5);
            glow.GradientOrigin = new Point(0.5, 0.5);
            glow.RadiusX = 0.5;
            glow.RadiusY = 0.5;
            glow.GradientStops.Add(new GradientStop(
                Color.FromArgb(128, color.R, color.G, color.B), 0));
            glow.GradientStops.Add(new GradientStop(
                Color.FromArgb(22, color.R, color.G, color.B), 0.56));
            glow.GradientStops.Add(new GradientStop(
                Color.FromArgb(0, color.R, color.G, color.B), 1));
            glow.Freeze();

            Ellipse ellipse = new Ellipse();
            ellipse.Width = size;
            ellipse.Height = size;
            ellipse.Fill = glow;
            ellipse.Opacity = opacity;
            ellipse.IsHitTestVisible = false;
            return ellipse;
        }

        private FrameworkElement BuildTitleBar()
        {
            Grid titleBar = new Grid();
            titleBar.Margin = new Thickness(22, 0, 16, 0);
            titleBar.Background = Brushes.Transparent;
            titleBar.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            titleBar.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            titleBar.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            titleBar.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            titleBar.MouseLeftButtonDown += OnTitleBarMouseLeftButtonDown;

            StackPanel brand = new StackPanel();
            brand.Orientation = Orientation.Horizontal;
            brand.VerticalAlignment = VerticalAlignment.Center;

            brand.Children.Add(UiKit.Logo());

            StackPanel wordmark = new StackPanel();
            wordmark.Margin = new Thickness(10, 0, 0, 0);
            wordmark.VerticalAlignment = VerticalAlignment.Center;
            wordmark.Children.Add(UiKit.Label("KeyShift", 15.0, UiKit.TextPrimary, FontWeights.SemiBold));
            TextBlock descriptor = UiKit.Label("键盘布局切换器", 9.3, UiKit.TextMuted);
            descriptor.Margin = new Thickness(0, 1, 0, 0);
            wordmark.Children.Add(descriptor);
            brand.Children.Add(wordmark);
            Grid.SetColumn(brand, 0);
            titleBar.Children.Add(brand);

            StackPanel right = new StackPanel();
            right.Orientation = Orientation.Horizontal;
            right.VerticalAlignment = VerticalAlignment.Center;
            right.HorizontalAlignment = HorizontalAlignment.Right;

            Border permissionPill = new Border();
            permissionPill.CornerRadius = new CornerRadius(99);
            permissionPill.Background = UiKit.Brush("#09FFFFFF");
            permissionPill.BorderBrush = UiKit.Brush("#14FFFFFF");
            permissionPill.BorderThickness = new Thickness(1);
            permissionPill.Padding = new Thickness(10, 5, 10, 5);
            permissionPill.Margin = new Thickness(0, 0, 10, 0);
            _headerPermissionPill = permissionPill;

            StackPanel permissionContent = new StackPanel();
            permissionContent.Orientation = Orientation.Horizontal;

            _headerPermissionDot = new Ellipse();
            _headerPermissionDot.Width = 5;
            _headerPermissionDot.Height = 5;
            _headerPermissionDot.VerticalAlignment = VerticalAlignment.Center;
            _headerPermissionDot.Margin = new Thickness(0, 0, 6, 0);
            permissionContent.Children.Add(_headerPermissionDot);

            _headerPermissionText = UiKit.Label("标准权限", 9.4, UiKit.TextSecondary);
            _headerPermissionText.VerticalAlignment = VerticalAlignment.Center;
            permissionContent.Children.Add(_headerPermissionText);

            permissionPill.Child = permissionContent;
            permissionPill.ToolTip = "普通权限启动；切换布局时会请求管理员授权。";
            right.Children.Add(permissionPill);

            Button minimize = CreateWindowButton(false);
            minimize.Click += delegate { WindowState = WindowState.Minimized; };
            right.Children.Add(minimize);

            Button close = CreateWindowButton(true);
            close.Margin = new Thickness(2, 0, 0, 0);
            close.Click += delegate { Close(); };
            right.Children.Add(close);

            Grid.SetColumn(right, 2);
            Grid.SetColumnSpan(right, 2);
            titleBar.Children.Add(right);

            return titleBar;
        }

        private Button CreateWindowButton(bool close)
        {
            Button button = new Button();
            button.Width = 36;
            button.Height = 28;
            button.Background = Brushes.Transparent;
            button.BorderThickness = new Thickness(0);
            button.Cursor = Cursors.Hand;
            button.Focusable = false;
            button.Template = close ? UiKit.CloseButtonTemplate : UiKit.WindowButtonTemplate;
            button.Content = UiKit.WindowGlyph(close, UiKit.Brush(close ? "#A9B5C7" : "#8E9BAF"));
            button.ToolTip = close ? "关闭" : "最小化";
            AutomationProperties.SetName(button, close ? "关闭" : "最小化");
            ToolTipService.SetInitialShowDelay(button, 350);
            return button;
        }

        private FrameworkElement BuildBody()
        {
            Grid body = new Grid();
            body.Margin = new Thickness(26, 7, 26, 14);
            body.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            body.RowDefinitions.Add(new RowDefinition { Height = new GridLength(14) });
            body.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            body.RowDefinitions.Add(new RowDefinition { Height = new GridLength(10) });
            body.RowDefinitions.Add(new RowDefinition { Height = new GridLength(20) });

            FrameworkElement hero = BuildHero();
            Grid.SetRow(hero, 0);
            body.Children.Add(hero);

            Grid cards = new Grid();
            cards.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(532) });
            cards.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(16) });
            cards.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(300) });

            Border currentCard = BuildCurrentCard();
            Grid.SetColumn(currentCard, 0);
            cards.Children.Add(currentCard);

            Border actionCard = BuildActionCard();
            Grid.SetColumn(actionCard, 2);
            cards.Children.Add(actionCard);

            Grid.SetRow(cards, 2);
            body.Children.Add(cards);

            FrameworkElement footer = BuildFooter();
            Grid.SetRow(footer, 4);
            body.Children.Add(footer);

            return body;
        }

        private FrameworkElement BuildHero()
        {
            Grid hero = new Grid();
            hero.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            hero.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            StackPanel copy = new StackPanel();
            copy.VerticalAlignment = VerticalAlignment.Center;
            copy.Children.Add(UiKit.Label(
                "键盘布局切换器", 24.0, UiKit.TextPrimary, FontWeights.SemiBold));

            TextBlock subtitle = UiKit.Label(
                "让 Microsoft 拼音使用英式或美式键盘布局", 11.3, UiKit.TextSecondary);
            subtitle.Margin = new Thickness(0, 4, 0, 0);
            copy.Children.Add(subtitle);
            Grid.SetColumn(copy, 0);
            hero.Children.Add(copy);

            Border scope = new Border();
            scope.CornerRadius = new CornerRadius(99);
            scope.Background = UiKit.Brush("#09FFFFFF");
            scope.BorderBrush = UiKit.Brush("#16FFFFFF");
            scope.BorderThickness = new Thickness(1);
            scope.Padding = new Thickness(11, 6, 11, 6);
            scope.VerticalAlignment = VerticalAlignment.Center;

            StackPanel scopeContent = new StackPanel();
            scopeContent.Orientation = Orientation.Horizontal;

            Ellipse scopeDot = new Ellipse();
            scopeDot.Width = 5;
            scopeDot.Height = 5;
            scopeDot.Fill = UiKit.Blue;
            scopeDot.VerticalAlignment = VerticalAlignment.Center;
            scopeDot.Margin = new Thickness(0, 0, 7, 0);
            scopeContent.Children.Add(scopeDot);

            TextBlock code = UiKit.MonoLabel("00000804", 9.2, UiKit.TextSecondary);
            code.VerticalAlignment = VerticalAlignment.Center;
            scopeContent.Children.Add(code);

            TextBlock scopeNote = UiKit.Label("· 仅此布局", 9.2, UiKit.TextMuted);
            scopeNote.Margin = new Thickness(5, 0, 0, 0);
            scopeNote.VerticalAlignment = VerticalAlignment.Center;
            scopeContent.Children.Add(scopeNote);

            scope.Child = scopeContent;
            scope.ToolTip = "仅修改：\nHKEY_LOCAL_MACHINE\\" + KeyboardLayoutService.SubKeyPath;
            Grid.SetColumn(scope, 1);
            hero.Children.Add(scope);

            return hero;
        }

        private Border BuildCurrentCard()
        {
            Border card = UiKit.Card();
            card.Padding = new Thickness(22, 18, 22, 16);

            Grid content = new Grid();
            content.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            content.RowDefinitions.Add(new RowDefinition { Height = new GridLength(14) });
            content.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            content.RowDefinitions.Add(new RowDefinition { Height = new GridLength(16) });
            content.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1) });
            content.RowDefinitions.Add(new RowDefinition { Height = new GridLength(16) });
            content.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            content.RowDefinitions.Add(new RowDefinition { Height = new GridLength(9) });
            content.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            content.RowDefinitions.Add(new RowDefinition { Height = new GridLength(9) });
            content.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            content.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            content.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            Grid header = new Grid();
            header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            TextBlock headerLabel = UiKit.Label(
                "当前布局", 11.2, UiKit.TextSecondary, FontWeights.SemiBold);
            headerLabel.VerticalAlignment = VerticalAlignment.Center;
            header.Children.Add(headerLabel);

            _statusPill = BuildStatusPill();
            Grid.SetColumn(_statusPill, 1);
            header.Children.Add(_statusPill);

            Grid.SetRow(header, 0);
            content.Children.Add(header);

            Grid current = new Grid();
            current.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(60) });
            current.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(14) });
            current.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            _layoutBadge = new Border();
            _layoutBadge.Width = 60;
            _layoutBadge.Height = 60;
            _layoutBadge.CornerRadius = new CornerRadius(16);
            _layoutBadge.BorderThickness = new Thickness(1);
            _layoutBadge.BorderBrush = UiKit.Brush("#2EFFFFFF");
            _layoutBadge.SnapsToDevicePixels = true;
            _layoutBadge.VerticalAlignment = VerticalAlignment.Center;

            _layoutBadgeText = UiKit.Label("UK", 18.0, Brushes.White, FontWeights.SemiBold);
            _layoutBadgeText.HorizontalAlignment = HorizontalAlignment.Center;
            _layoutBadgeText.VerticalAlignment = VerticalAlignment.Center;
            _layoutBadge.Child = _layoutBadgeText;
            Grid.SetColumn(_layoutBadge, 0);
            current.Children.Add(_layoutBadge);

            StackPanel identity = new StackPanel();
            identity.VerticalAlignment = VerticalAlignment.Center;
            _layoutName = UiKit.Label("英式键盘", 25.0, UiKit.TextPrimary, FontWeights.SemiBold);
            identity.Children.Add(_layoutName);

            StackPanel meta = new StackPanel();
            meta.Orientation = Orientation.Horizontal;
            meta.Margin = new Thickness(1, 3, 0, 0);
            _layoutCountry = UiKit.Label("United Kingdom", 10.6, UiKit.TextSecondary);
            _layoutCountry.VerticalAlignment = VerticalAlignment.Center;
            meta.Children.Add(_layoutCountry);

            TextBlock separator = UiKit.Label("·", 10.6, UiKit.TextFaint);
            separator.Margin = new Thickness(7, 0, 7, 0);
            separator.VerticalAlignment = VerticalAlignment.Center;
            meta.Children.Add(separator);

            _layoutFile = UiKit.MonoLabel("KBDUK.DLL", 9.8, UiKit.Brush(UiKit.UkAccent));
            _layoutFile.VerticalAlignment = VerticalAlignment.Center;
            meta.Children.Add(_layoutFile);
            identity.Children.Add(meta);

            Grid.SetColumn(identity, 2);
            current.Children.Add(identity);

            Grid.SetRow(current, 2);
            content.Children.Add(current);

            Border divider = UiKit.Divider();
            Grid.SetRow(divider, 4);
            content.Children.Add(divider);

            Grid sectionHeader = new Grid();
            sectionHeader.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            sectionHeader.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            TextBlock sectionTitle = UiKit.Label(
                "键位差异", 11.0, UiKit.TextSecondary, FontWeights.SemiBold);
            sectionHeader.Children.Add(sectionTitle);
            TextBlock sectionHint = UiKit.Label("SHIFT 组合键", 8.6, UiKit.TextFaint);
            sectionHint.FontFamily = UiKit.MonoFont;
            sectionHint.VerticalAlignment = VerticalAlignment.Center;
            Grid.SetColumn(sectionHint, 1);
            sectionHeader.Children.Add(sectionHint);
            Grid.SetRow(sectionHeader, 6);
            content.Children.Add(sectionHeader);

            _keysHost = new Grid();
            _keysHost.Height = 96;
            _keysHost.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            _keysHost.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            _keysHost.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            Grid.SetRow(_keysHost, 8);
            content.Children.Add(_keysHost);

            _keyCaption = UiKit.Label(string.Empty, 10.0, UiKit.TextMuted);
            _keyCaption.TextWrapping = TextWrapping.Wrap;
            _keyCaption.LineHeight = 16;
            _keyCaption.LineStackingStrategy = LineStackingStrategy.BlockLineHeight;
            _keyCaption.MinHeight = 32;
            Grid.SetRow(_keyCaption, 10);
            content.Children.Add(_keyCaption);

            StackPanel note = new StackPanel();
            note.Orientation = Orientation.Horizontal;
            note.VerticalAlignment = VerticalAlignment.Center;
            FrameworkElement noteIcon = UiKit.ShieldIcon(UiKit.TextFaint, 11);
            noteIcon.VerticalAlignment = VerticalAlignment.Center;
            noteIcon.Margin = new Thickness(0, 0, 7, 0);            note.Children.Add(noteIcon);
            TextBlock noteText = UiKit.Label(
                "仅修改注册表，不替换系统文件", 9.6, UiKit.TextMuted);
            noteText.VerticalAlignment = VerticalAlignment.Center;
            note.Children.Add(noteText);
            Grid.SetRow(note, 12);
            content.Children.Add(note);

            card.Child = content;
            return card;
        }

        private Border BuildStatusPill()
        {
            StackPanel content = new StackPanel();
            content.Orientation = Orientation.Horizontal;

            _statusDot = new Ellipse();
            _statusDot.Width = 5;
            _statusDot.Height = 5;
            _statusDot.VerticalAlignment = VerticalAlignment.Center;
            _statusDot.Margin = new Thickness(0, 0, 6, 0);
            content.Children.Add(_statusDot);

            _statusText = UiKit.Label("已启用", 9.2, UiKit.Green, FontWeights.SemiBold);
            _statusText.VerticalAlignment = VerticalAlignment.Center;
            content.Children.Add(_statusText);

            Border pill = new Border();
            pill.CornerRadius = new CornerRadius(99);
            pill.BorderThickness = new Thickness(1);
            pill.Padding = new Thickness(9, 4, 9, 4);
            pill.Child = content;
            return pill;
        }

        private Border BuildActionCard()
        {
            Border card = UiKit.Card();
            card.Padding = new Thickness(20, 18, 20, 16);

            Grid content = new Grid();
            content.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            content.RowDefinitions.Add(new RowDefinition { Height = new GridLength(14) });
            content.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            content.RowDefinitions.Add(new RowDefinition { Height = new GridLength(14) });
            content.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            content.RowDefinitions.Add(new RowDefinition { Height = new GridLength(12) });
            content.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            content.RowDefinitions.Add(new RowDefinition { Height = new GridLength(12) });
            content.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            content.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            content.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            Grid header = new Grid();
            header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            TextBlock title = UiKit.Label(
                "一键切换", 13.0, UiKit.TextPrimary, FontWeights.SemiBold);
            header.Children.Add(title);
            TextBlock tag = UiKit.MonoLabel("REGISTRY", 8.0, UiKit.TextFaint);
            tag.VerticalAlignment = VerticalAlignment.Center;
            Grid.SetColumn(tag, 1);
            header.Children.Add(tag);
            Grid.SetRow(header, 0);
            content.Children.Add(header);

            Grid targetContent = new Grid();
            targetContent.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(46) });
            targetContent.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(11) });
            targetContent.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            _targetBadge = new Border();
            _targetBadge.Width = 46;
            _targetBadge.Height = 46;
            _targetBadge.CornerRadius = new CornerRadius(13);
            _targetBadge.BorderThickness = new Thickness(1);
            _targetBadge.BorderBrush = UiKit.Brush("#2EFFFFFF");
            _targetBadge.VerticalAlignment = VerticalAlignment.Center;

            _targetBadgeText = UiKit.Label("US", 14.0, Brushes.White, FontWeights.SemiBold);
            _targetBadgeText.HorizontalAlignment = HorizontalAlignment.Center;
            _targetBadgeText.VerticalAlignment = VerticalAlignment.Center;
            _targetBadge.Child = _targetBadgeText;
            targetContent.Children.Add(_targetBadge);

            StackPanel targetCopy = new StackPanel();
            targetCopy.VerticalAlignment = VerticalAlignment.Center;
            TextBlock targetLabel = UiKit.Label("目标布局", 8.7, UiKit.TextFaint);
            targetCopy.Children.Add(targetLabel);

            _targetName = UiKit.Label("美式键盘", 17.0, UiKit.TextPrimary, FontWeights.SemiBold);
            _targetName.Margin = new Thickness(0, 1, 0, 0);
            targetCopy.Children.Add(_targetName);

            _targetMeta = UiKit.Label("United States · KBDUS.DLL", 8.8, UiKit.TextSecondary);
            _targetMeta.Margin = new Thickness(0, 2, 0, 0);
            _targetMeta.TextTrimming = TextTrimming.CharacterEllipsis;
            targetCopy.Children.Add(_targetMeta);

            Grid.SetColumn(targetCopy, 2);
            targetContent.Children.Add(targetCopy);

            Border targetBox = UiKit.RoundedBorder(
                targetContent,
                UiKit.Brush(UiKit.InnerCard),
                UiKit.Brush(UiKit.InnerBorder),
                14,
                new Thickness(13, 12, 13, 12));
            targetBox.MinHeight = 90;
            Grid.SetRow(targetBox, 2);
            content.Children.Add(targetBox);

            _applyButton = new Button();
            _applyButton.Height = 54;
            _applyButton.BorderThickness = new Thickness(0);
            _applyButton.Padding = new Thickness(18, 0, 14, 0);
            _applyButton.Cursor = Cursors.Hand;
            _applyButton.Template = UiKit.PrimaryButtonTemplate;
            _applyButton.HorizontalContentAlignment = HorizontalAlignment.Stretch;
            _applyButton.FocusVisualStyle = null;
            _applyButton.Click += OnApplyClicked;
            AutomationProperties.SetName(_applyButton, "切换键盘布局");
            ToolTipService.SetInitialShowDelay(_applyButton, 400);

            Grid applyContent = new Grid();
            applyContent.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            applyContent.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            _applyLabel = UiKit.Label(
                "立即切换为美式键盘", 13.3, Brushes.White, FontWeights.SemiBold);
            _applyLabel.VerticalAlignment = VerticalAlignment.Center;
            _applyLabel.TextTrimming = TextTrimming.CharacterEllipsis;
            applyContent.Children.Add(_applyLabel);

            Border arrowBubble = new Border();
            arrowBubble.Width = 28;
            arrowBubble.Height = 28;
            arrowBubble.CornerRadius = new CornerRadius(14);
            arrowBubble.Background = UiKit.Brush("#24FFFFFF");
            arrowBubble.VerticalAlignment = VerticalAlignment.Center;
            FrameworkElement arrow = UiKit.ArrowIcon(Brushes.White, 14);
            arrow.HorizontalAlignment = HorizontalAlignment.Center;
            arrow.VerticalAlignment = VerticalAlignment.Center;
            arrowBubble.Child = arrow;
            Grid.SetColumn(arrowBubble, 1);
            applyContent.Children.Add(arrowBubble);

            _applyButton.Content = applyContent;
            Grid.SetRow(_applyButton, 4);
            content.Children.Add(_applyButton);

            StackPanel hints = new StackPanel();
            hints.Children.Add(BuildHintRow(
                UiKit.ShieldIcon(UiKit.TextFaint, 11),
                out _permissionHint,
                "切换时将请求管理员授权"));
            Grid.SetRow(hints, 6);
            content.Children.Add(hints);

            Color amber = UiKit.Hex("#FFC46B");
            Border restartInfo = new Border();
            restartInfo.CornerRadius = new CornerRadius(12);
            restartInfo.Background = UiKit.Tint(amber, 12);
            restartInfo.BorderBrush = UiKit.Tint(amber, 40);
            restartInfo.BorderThickness = new Thickness(1);
            restartInfo.Padding = new Thickness(12, 10, 12, 10);

            Grid restartContent = new Grid();
            restartContent.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(13) });
            restartContent.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            FrameworkElement clock = UiKit.ClockIcon(UiKit.Brush("#D9AE68"), 11);
            clock.VerticalAlignment = VerticalAlignment.Top;
            clock.Margin = new Thickness(0, 2, 0, 0);
            restartContent.Children.Add(clock);

            StackPanel restartCopy = new StackPanel();
            restartCopy.Margin = new Thickness(7, 0, 0, 0);
            TextBlock restartLabel = UiKit.Label("生效方式", 9.0, UiKit.Amber, FontWeights.SemiBold);
            restartCopy.Children.Add(restartLabel);
            TextBlock restartText = UiKit.Label(
                "Windows 在登录时加载键盘布局，写入后请注销或重启电脑。",
                9.2,
                UiKit.TextSecondary);
            restartText.TextWrapping = TextWrapping.Wrap;
            restartText.LineHeight = 14.5;
            restartText.LineStackingStrategy = LineStackingStrategy.BlockLineHeight;
            restartText.Margin = new Thickness(0, 3, 0, 0);
            restartCopy.Children.Add(restartText);
            Grid.SetColumn(restartCopy, 1);
            restartContent.Children.Add(restartCopy);

            restartInfo.Child = restartContent;
            Grid.SetRow(restartInfo, 8);
            content.Children.Add(restartInfo);

            StackPanel footer = new StackPanel();
            footer.Children.Add(UiKit.Divider());
            Grid links = new Grid();
            links.Margin = new Thickness(0, 9, 0, 0);
            links.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            links.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            _resetButton = new Button();
            _resetButton.Template = UiKit.TextButtonTemplate;
            _resetButton.Background = Brushes.Transparent;
            _resetButton.BorderThickness = new Thickness(0);
            _resetButton.Padding = new Thickness(0);
            _resetButton.Cursor = Cursors.Hand;
            _resetButton.HorizontalContentAlignment = HorizontalAlignment.Left;
            _resetButton.VerticalAlignment = VerticalAlignment.Center;
            _resetButton.Click += OnResetClicked;
            _resetLabel = UiKit.Label(DefaultResetText, 9.5, UiKit.TextMuted);
            _resetButton.Content = _resetLabel;
            _resetButton.ToolTip = "将 Layout File 恢复为 KBDUS.DLL，并同步还原美式布局名称。";
            links.Children.Add(_resetButton);

            Button refresh = new Button();
            refresh.Template = UiKit.TextButtonTemplate;
            refresh.Background = Brushes.Transparent;
            refresh.BorderThickness = new Thickness(0);
            refresh.Padding = new Thickness(6, 3, 0, 3);
            refresh.Cursor = Cursors.Hand;
            refresh.HorizontalContentAlignment = HorizontalAlignment.Right;
            refresh.VerticalAlignment = VerticalAlignment.Center;
            refresh.Click += delegate { RefreshState(true); };
            TextBlock refreshLabel = UiKit.Label("F5  刷新", 9.3, UiKit.TextFaint);
            refresh.Content = refreshLabel;
            refresh.ToolTip = "重新读取当前布局状态（F5）";
            Grid.SetColumn(refresh, 1);
            links.Children.Add(refresh);

            footer.Children.Add(links);
            Grid.SetRow(footer, 10);
            content.Children.Add(footer);

            card.Child = content;
            return card;
        }

        private Grid BuildHintRow(FrameworkElement icon, out TextBlock text, string caption)
        {
            Grid row = new Grid();
            row.Margin = new Thickness(0);
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(13) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            icon.VerticalAlignment = VerticalAlignment.Center;
            row.Children.Add(icon);

            text = UiKit.Label(caption, 9.7, UiKit.TextMuted);
            text.VerticalAlignment = VerticalAlignment.Center;
            text.Margin = new Thickness(7, 0, 0, 0);
            Grid.SetColumn(text, 1);
            row.Children.Add(text);
            return row;
        }

        private FrameworkElement BuildFooter()
        {
            Grid footer = new Grid();
            footer.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            footer.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            Button copyPath = new Button();
            copyPath.Template = UiKit.TextButtonTemplate;
            copyPath.Background = Brushes.Transparent;
            copyPath.BorderThickness = new Thickness(0);
            copyPath.Padding = new Thickness(6, 3, 8, 3);
            copyPath.Margin = new Thickness(-6, 0, 0, 0);
            copyPath.Cursor = Cursors.Hand;
            copyPath.HorizontalContentAlignment = HorizontalAlignment.Left;
            copyPath.VerticalAlignment = VerticalAlignment.Center;
            copyPath.Click += OnCopyPathClicked;
            copyPath.ToolTip = "点击复制完整注册表路径";

            StackPanel pathContent = new StackPanel();
            pathContent.Orientation = Orientation.Horizontal;
            Ellipse dot = new Ellipse();
            dot.Width = 4;
            dot.Height = 4;
            dot.Fill = UiKit.TextFaint;
            dot.VerticalAlignment = VerticalAlignment.Center;
            dot.Margin = new Thickness(0, 0, 7, 0);
            pathContent.Children.Add(dot);
            TextBlock path = UiKit.MonoLabel(
                @"HKLM\SYSTEM\CurrentControlSet\Control\Keyboard Layouts\00000804",
                9.1,
                UiKit.TextMuted);
            path.VerticalAlignment = VerticalAlignment.Center;
            pathContent.Children.Add(path);
            copyPath.Content = pathContent;
            footer.Children.Add(copyPath);

            TextBlock version = UiKit.MonoLabel("KeyShift 1.0", 8.8, UiKit.TextFaint);
            version.VerticalAlignment = VerticalAlignment.Center;
            version.HorizontalAlignment = HorizontalAlignment.Right;
            Grid.SetColumn(version, 1);
            footer.Children.Add(version);

            return footer;
        }

        private Border BuildToast()
        {
            _toastSlide = new TranslateTransform(0, 12);

            _toastGlyphBubble = new Border();
            _toastGlyphBubble.Width = 22;
            _toastGlyphBubble.Height = 22;
            _toastGlyphBubble.CornerRadius = new CornerRadius(11);
            _toastGlyphBubble.VerticalAlignment = VerticalAlignment.Center;

            _toastGlyph = UiKit.Label("✓", 11.0, UiKit.Green, FontWeights.Bold);
            _toastGlyph.HorizontalAlignment = HorizontalAlignment.Center;
            _toastGlyph.VerticalAlignment = VerticalAlignment.Center;
            _toastGlyphBubble.Child = _toastGlyph;

            _toastText = UiKit.Label(string.Empty, 10.2, UiKit.TextPrimary);
            _toastText.TextWrapping = TextWrapping.Wrap;
            _toastText.MaxWidth = 560;
            _toastText.VerticalAlignment = VerticalAlignment.Center;
            _toastText.Margin = new Thickness(9, 0, 0, 0);

            StackPanel content = new StackPanel();
            content.Orientation = Orientation.Horizontal;
            content.Children.Add(_toastGlyphBubble);
            content.Children.Add(_toastText);

            Border toast = new Border();
            toast.Visibility = Visibility.Collapsed;
            toast.Opacity = 0;
            toast.HorizontalAlignment = HorizontalAlignment.Center;
            toast.VerticalAlignment = VerticalAlignment.Bottom;
            toast.Margin = new Thickness(0, 0, 0, 14);
            toast.Padding = new Thickness(12, 9, 15, 9);
            toast.CornerRadius = new CornerRadius(11);
            toast.BorderThickness = new Thickness(1);
            toast.Background = UiKit.Brush("#FF1A2434");
            toast.BorderBrush = UiKit.Brush("#33445E");
            toast.Child = content;
            toast.RenderTransform = _toastSlide;
            toast.IsHitTestVisible = false;
            Panel.SetZIndex(toast, 50);
            return toast;
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            StartEntranceAnimation();

            if (!string.IsNullOrEmpty(_startupMessage))
            {
                string message = _startupMessage;
                ToastKind kind = _startupToastKind;
                Dispatcher.BeginInvoke(
                    new Action(delegate { ShowToast(message, kind); }),
                    DispatcherPriority.Background);
            }

#if PREVIEW
            if (!string.IsNullOrEmpty(_options.ScreenshotPath))
            {
                _screenshotTimer = new DispatcherTimer();
                _screenshotTimer.Interval = TimeSpan.FromMilliseconds(850);
                _screenshotTimer.Tick += OnScreenshotTimerTick;
                _screenshotTimer.Start();
            }
#endif
        }

        private void StartEntranceAnimation()
        {
            _rootSlide = new TranslateTransform(0, 7);
            _designRoot.RenderTransform = _rootSlide;
            _designRoot.Opacity = 0;

            DoubleAnimation fade = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(260));
            fade.EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut };
            _designRoot.BeginAnimation(OpacityProperty, fade);

            DoubleAnimation rise = new DoubleAnimation(7, 0, TimeSpan.FromMilliseconds(300));
            rise.EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut };
            _rootSlide.BeginAnimation(TranslateTransform.YProperty, rise);
        }

        private void OnPreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.F5)
            {
                RefreshState(true);
                e.Handled = true;
            }
            else if (e.Key == Key.Escape)
            {
                Close();
                e.Handled = true;
            }
        }

        private void OnTitleBarMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ButtonState != MouseButtonState.Pressed)
            {
                return;
            }

            if (FindVisualAncestor<Button>(e.OriginalSource as DependencyObject) != null)
            {
                return;
            }

            try
            {
                DragMove();
            }
            catch (InvalidOperationException)
            {
            }
        }

        private static T FindVisualAncestor<T>(DependencyObject source) where T : DependencyObject
        {
            DependencyObject current = source;
            while (current != null)
            {
                T match = current as T;
                if (match != null)
                {
                    return match;
                }

                current = VisualTreeHelper.GetParent(current);
            }

            return null;
        }

        private LayoutState SafeReadState()
        {
            _initialReadError = null;
            try
            {
                return KeyboardLayoutService.Read();
            }
            catch (Exception ex)
            {
                _initialReadError = ex.Message;
                return new LayoutState(LayoutKind.Unknown, string.Empty, string.Empty);
            }
        }

        private void ApplyStartupTarget(LayoutKind target)
        {
            if (!_isElevated)
            {
                _startupToastKind = ToastKind.Error;
                _startupMessage = "需要管理员权限才能修改注册表。请以管理员身份运行 KeyShift。";
                return;
            }

            try
            {
                KeyboardLayoutService.Apply(target);
                _pendingRestart = true;
                _state = ReadAfterApply(target);
                _startupToastKind = ToastKind.Success;
                _startupMessage = "已切换为" + DisplayNameFor(target)
                    + "。注销或重启 Windows 后生效。";
            }
            catch (Exception ex)
            {
                _startupToastKind = ToastKind.Error;
                _startupMessage = FriendlyError(ex);
                try
                {
                    _state = KeyboardLayoutService.Read();
                }
                catch
                {
                }
            }
        }

        private void RefreshUi()
        {
            if (_layoutName == null)
            {
                return;
            }

            LayoutKind currentKind = _state.Kind;
            Color currentAccent = UiKit.AccentFor(currentKind);

            _layoutBadge.Background = UiKit.AccentGradientFor(currentKind);
            _layoutBadgeText.Text = _state.Badge;
            _layoutBadge.ToolTip = string.IsNullOrEmpty(_state.LayoutText)
                ? null
                : "Layout Text: " + _state.LayoutText;

            _layoutName.Text = _state.DisplayName;
            _layoutCountry.Text = _state.CountryName;
            _layoutFile.Text = _state.FileName;
            _layoutFile.Foreground = UiKit.Brush(currentAccent);

            UpdateStatusPill();

            LayoutKind target = GetTargetKind();
            _targetBadge.Background = UiKit.AccentGradientFor(target);
            _targetBadgeText.Text = target == LayoutKind.Uk ? "UK" : "US";
            _targetName.Text = DisplayNameFor(target);
            _targetMeta.Text = CountryFor(target) + " · " + FileNameFor(target);
            _applyLabel.Text = "立即切换为" + DisplayNameFor(target);
            _applyButton.Background = UiKit.AccentGradientFor(target);
            _applyButton.ToolTip = "将 Layout File 改为 " + FileNameFor(target)
                + "\n修改后需要注销或重启 Windows 生效。";
            AutomationProperties.SetHelpText(
                _applyButton, "将键盘布局切换为" + DisplayNameFor(target));

            RebuildKeycaps(currentKind, currentAccent);
            if (_resetArmed && _state.Kind == LayoutKind.Us)
            {
                _resetArmed = false;
                _resetTimer.Stop();
            }

            UpdatePermissionUi();
            UpdateResetButton();
        }

        private void UpdateStatusPill()
        {
            Color accent = UiKit.AccentFor(_state.Kind);
            Color amber = UiKit.Hex("#FFC46B");
            Color statusColor = _pendingRestart ? amber : (_state.IsKnown ? accent : amber);
            string statusLabel = _pendingRestart ? "重启后生效" : (_state.IsKnown ? "已启用" : "未识别");

            _statusDot.Fill = UiKit.Brush(statusColor);
            _statusText.Text = statusLabel;
            _statusText.Foreground = UiKit.Brush(statusColor);
            _statusPill.Background = UiKit.Tint(statusColor, 24);
            _statusPill.BorderBrush = UiKit.Tint(statusColor, 58);
            _statusPill.ToolTip = _pendingRestart
                ? "注册表已更新；重新登录或重启 Windows 后，新布局才会被加载。"
                : "根据 HKLM\\SYSTEM\\CurrentControlSet\\Control\\Keyboard Layouts\\00000804 的 Layout File 判断。";
        }

        private void RebuildKeycaps(LayoutKind kind, Color accent)
        {
            _keysHost.Children.Clear();

            string[] labels = new string[] { "SHIFT + 2", "SHIFT + 3", "SHIFT + '" };
            string[] symbols;
            if (kind == LayoutKind.Uk)
            {
                symbols = new string[] { "\"", "£", "@" };
                _keyCaption.Text = "英式布局中，Shift + 2 为 \"，Shift + 3 为 £；@ 位于引号键。";
            }
            else if (kind == LayoutKind.Us)
            {
                symbols = new string[] { "@", "#", "\"" };
                _keyCaption.Text = "美式布局中，Shift + 2 为 @，Shift + 3 为 #；\" 位于引号键。";
            }
            else
            {
                symbols = new string[] { "—", "—", "—" };
                _keyCaption.Text = "无法识别当前 Layout File，请点击 F5 刷新或检查注册表。";
            }

            Brush accentBrush = UiKit.Brush(accent);
            for (int i = 0; i < 3; i++)
            {
                Border key = BuildKeycap(labels[i], symbols[i], accentBrush);
                if (i > 0)
                {
                    key.Margin = new Thickness(8, 0, 0, 0);
                }

                Grid.SetColumn(key, i);
                _keysHost.Children.Add(key);
            }
        }

        private Border BuildKeycap(string label, string symbol, Brush accent)
        {
            Grid content = new Grid();
            content.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            content.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

            TextBlock capLabel = UiKit.MonoLabel(label, 8.4, UiKit.TextMuted);
            capLabel.HorizontalAlignment = HorizontalAlignment.Center;
            content.Children.Add(capLabel);

            TextBlock capSymbol = UiKit.Label(symbol, 24.0, accent, FontWeights.SemiBold);
            capSymbol.HorizontalAlignment = HorizontalAlignment.Center;
            capSymbol.VerticalAlignment = VerticalAlignment.Center;
            Grid.SetRow(capSymbol, 1);
            content.Children.Add(capSymbol);

            LinearGradientBrush background = UiKit.Gradient(
                UiKit.Hex("#182233"), UiKit.Hex("#121A27"));
            Border key = new Border();
            key.Height = 96;
            key.CornerRadius = new CornerRadius(12);
            key.Background = background;
            key.BorderBrush = UiKit.Brush("#27354A");
            key.BorderThickness = new Thickness(1);
            key.Padding = new Thickness(8, 9, 8, 9);
            key.Child = content;
            return key;
        }

        private void UpdatePermissionUi()
        {
            bool elevated = _isElevated;
            if (elevated)
            {
                _headerPermissionDot.Fill = UiKit.Green;
                _headerPermissionText.Text = "管理员";
                _headerPermissionText.Foreground = UiKit.TextSecondary;
                _permissionHint.Text = "管理员权限已就绪，可直接切换";
                _permissionHint.Foreground = UiKit.TextSecondary;
                _headerPermissionPill.ToolTip = "已获得管理员权限，可直接修改系统注册表。";
            }
            else
            {
                _headerPermissionDot.Fill = UiKit.TextMuted;
                _headerPermissionText.Text = "标准权限";
                _headerPermissionText.Foreground = UiKit.TextSecondary;
                _permissionHint.Text = "切换时将请求管理员授权";
                _permissionHint.Foreground = UiKit.TextMuted;
                _headerPermissionPill.ToolTip = "普通权限启动；切换布局时会请求管理员授权。";
            }
        }

        private void UpdateResetButton()
        {
            bool resetRelevant = _state.Kind != LayoutKind.Us;
            _resetButton.IsEnabled = resetRelevant;
            _resetLabel.Text = _resetArmed ? "再次点击确认恢复" : DefaultResetText;
            _resetLabel.Foreground = _resetArmed
                ? UiKit.Amber
                : (resetRelevant ? UiKit.TextMuted : UiKit.TextFaint);
        }

        private void OnApplyClicked(object sender, RoutedEventArgs e)
        {
            if (!RefreshState(false))
            {
                return;
            }

            LayoutKind target = GetTargetKind();
            ApplyTarget(target);
        }

        private void OnResetClicked(object sender, RoutedEventArgs e)
        {
            if (_state.Kind == LayoutKind.Us)
            {
                ShowToast("当前已是系统默认的美式布局。", ToastKind.Info);
                return;
            }

            if (!_resetArmed)
            {
                _resetArmed = true;
                _resetTimer.Stop();
                _resetTimer.Start();
                RefreshUi();
                ShowToast("再次点击“确认恢复”即可还原为美式布局。", ToastKind.Info);
                return;
            }

            _resetTimer.Stop();
            _resetArmed = false;
            RefreshUi();
            ApplyTarget(LayoutKind.Us);
        }

        private void OnResetTimerTick(object sender, EventArgs e)
        {
            _resetTimer.Stop();
            if (!_resetArmed)
            {
                return;
            }

            _resetArmed = false;
            UpdateResetButton();
        }

        private void ApplyTarget(LayoutKind target)
        {
            if (target == LayoutKind.Unknown)
            {
                return;
            }

            if (_state.Kind == target)
            {
                ShowToast("当前已经是" + DisplayNameFor(target) + "，无需切换。", ToastKind.Info);
                return;
            }

            string systemDirectory = Environment.GetFolderPath(Environment.SpecialFolder.System);
            string dllPath = Path.Combine(systemDirectory, FileNameFor(target));
            if (!File.Exists(dllPath))
            {
                ShowToast("系统中找不到 " + FileNameFor(target) + "，无法切换。", ToastKind.Error);
                return;
            }

            if (!_isElevated)
            {
                ElevationResult result = ElevationHelper.LaunchElevated(target);
                if (result.Started)
                {
                    Application.Current.Shutdown();
                    return;
                }

                if (result.Canceled)
                {
                    ShowToast("已取消管理员授权，布局未更改。", ToastKind.Info);
                }
                else
                {
                    ShowToast("无法请求管理员权限：" + result.ErrorMessage, ToastKind.Error);
                }

                return;
            }

            ExecuteApply(target);
        }

        private void ExecuteApply(LayoutKind target)
        {
            _applyButton.IsEnabled = false;
            Mouse.OverrideCursor = Cursors.Wait;
            try
            {
                KeyboardLayoutService.Apply(target);
                _pendingRestart = true;
                _resetArmed = false;
                _resetTimer.Stop();
                _state = ReadAfterApply(target);
                _isElevated = ElevationHelper.IsElevated;
                RefreshUi();
                ShowToast(
                    "已切换为" + DisplayNameFor(target)
                    + "。注销或重启 Windows 后生效。",
                    ToastKind.Success);
            }
            catch (Exception ex)
            {
                try
                {
                    _state = KeyboardLayoutService.Read();
                }
                catch
                {
                }

                _isElevated = ElevationHelper.IsElevated;
                RefreshUi();
                ShowToast(FriendlyError(ex), ToastKind.Error);
            }
            finally
            {
                Mouse.OverrideCursor = null;
                if (_applyButton != null)
                {
                    _applyButton.IsEnabled = true;
                }
            }
        }

        private LayoutState ReadAfterApply(LayoutKind target)
        {
            try
            {
                return KeyboardLayoutService.Read();
            }
            catch
            {
                return new LayoutState(target, FileNameFor(target), string.Empty);
            }
        }

        private bool RefreshState(bool notify)
        {
            try
            {
                LayoutState previous = _state;
                _state = KeyboardLayoutService.Read();
                RefreshUi();

                if (notify)
                {
                    bool changed = previous.Kind != _state.Kind
                        || !string.Equals(previous.LayoutFile, _state.LayoutFile, StringComparison.OrdinalIgnoreCase);
                    ShowToast(
                        changed ? "已获取最新布局状态。" : "布局状态无变化。",
                        ToastKind.Info);
                }

                return true;
            }
            catch (Exception ex)
            {
                ShowToast("无法读取当前布局：" + FriendlyError(ex), ToastKind.Error);
                return false;
            }
        }

        private LayoutKind GetTargetKind()
        {
            return _state.Kind == LayoutKind.Uk ? LayoutKind.Us : LayoutKind.Uk;
        }

        private static string DisplayNameFor(LayoutKind kind)
        {
            return kind == LayoutKind.Uk ? "英式键盘" : "美式键盘";
        }

        private static string CountryFor(LayoutKind kind)
        {
            return kind == LayoutKind.Uk ? "United Kingdom" : "United States";
        }

        private static string FileNameFor(LayoutKind kind)
        {
            return kind == LayoutKind.Uk
                ? KeyboardLayoutService.UkFileName
                : KeyboardLayoutService.UsFileName;
        }

        private void ShowToast(string message, ToastKind kind)
        {
            if (_toast == null)
            {
                return;
            }

            Color accent = kind == ToastKind.Success
                ? UiKit.Hex("#57E0B7")
                : kind == ToastKind.Error
                    ? UiKit.Hex("#FF7485")
                    : UiKit.Hex("#7E9BFF");

            _toastGeneration++;
            _toastText.Text = message;
            _toastGlyph.Text = kind == ToastKind.Success ? "✓" : kind == ToastKind.Error ? "!" : "i";
            _toastGlyph.Foreground = UiKit.Brush(accent);
            _toastGlyphBubble.Background = UiKit.Tint(accent, 34);
            _toast.BorderBrush = UiKit.Tint(accent, 92);
            _toast.Visibility = Visibility.Visible;
            _toast.Opacity = 0;
            _toastSlide.Y = 12;

            DoubleAnimation fade = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(180));
            _toast.BeginAnimation(OpacityProperty, fade);

            DoubleAnimation rise = new DoubleAnimation(12, 0, TimeSpan.FromMilliseconds(220));
            rise.EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut };
            _toastSlide.BeginAnimation(TranslateTransform.YProperty, rise);

            _toastTimer.Stop();
            _toastTimer.Interval = kind == ToastKind.Error
                ? TimeSpan.FromSeconds(7)
                : TimeSpan.FromSeconds(5);
            _toastTimer.Start();
        }

        private void OnToastTimerTick(object sender, EventArgs e)
        {
            _toastTimer.Stop();
            FadeOutToast();
        }

        private void FadeOutToast()
        {
            if (_toast == null || _toast.Visibility != Visibility.Visible)
            {
                return;
            }

            int generation = _toastGeneration;
            DoubleAnimation fade = new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(220));
            fade.Completed += delegate
            {
                if (generation == _toastGeneration)
                {
                    _toast.Visibility = Visibility.Collapsed;
                }
            };
            _toast.BeginAnimation(OpacityProperty, fade);
        }

        private void OnCopyPathClicked(object sender, RoutedEventArgs e)
        {
            try
            {
                Clipboard.SetText(KeyboardLayoutService.FullRegistryPath);
                ShowToast("已复制注册表路径。", ToastKind.Info);
            }
            catch (Exception ex)
            {
                ShowToast("复制失败：" + ex.Message, ToastKind.Error);
            }
        }

        private static string FriendlyError(Exception ex)
        {
            if (ex is UnauthorizedAccessException)
            {
                return "权限不足，布局未更改。请以管理员身份重试。";
            }

            if (ex is System.Security.SecurityException)
            {
                return "系统安全策略不允许修改注册表，布局未更改。";
            }

            if (ex is FileNotFoundException)
            {
                return ex.Message;
            }

            if (ex is IOException)
            {
                return "读写注册表时发生错误：" + ex.Message;
            }

            return "切换失败：" + ex.Message;
        }

        private void OnWindowClosed(object sender, EventArgs e)
        {
            _toastTimer.Stop();
            _resetTimer.Stop();
        }

#if PREVIEW
        private void OnScreenshotTimerTick(object sender, EventArgs e)
        {
            _screenshotTimer.Stop();
            CaptureScreenshot(_options.ScreenshotPath);
        }

        private void CaptureScreenshot(string path)
        {
            try
            {
                _designRoot.BeginAnimation(OpacityProperty, null);
                _designRoot.Opacity = 1;
                if (_rootSlide != null)
                {
                    _rootSlide.BeginAnimation(TranslateTransform.YProperty, null);
                    _rootSlide.Y = 0;
                }

                _designRoot.UpdateLayout();

                const double scale = 2.0;
                RenderTargetBitmap bitmap = new RenderTargetBitmap(
                    (int)Math.Round(DesignWidth * scale),
                    (int)Math.Round(DesignHeight * scale),
                    96 * scale,
                    96 * scale,
                    PixelFormats.Pbgra32);
                bitmap.Render(_designRoot);

                PngBitmapEncoder encoder = new PngBitmapEncoder();
                encoder.Frames.Add(BitmapFrame.Create(bitmap));

                string directory = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                using (FileStream stream = File.Create(path))
                {
                    encoder.Save(stream);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    "Preview screenshot failed:\n" + ex.Message,
                    "KeyShift Preview",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
            finally
            {
                Application.Current.Shutdown();
            }
        }
#endif
    }
}
