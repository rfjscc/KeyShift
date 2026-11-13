using System;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Threading;

[assembly: AssemblyTitle("KeyShift 键盘布局切换器")]
[assembly: AssemblyDescription("一键切换 Microsoft 拼音的英式 / 美式键盘布局")]
[assembly: AssemblyCompany("KeyShift")]
[assembly: AssemblyProduct("KeyShift")]
[assembly: AssemblyCopyright("Copyright © 2026 KeyShift")]
[assembly: AssemblyVersion("1.0.0.0")]
[assembly: AssemblyFileVersion("1.0.0.0")]
[assembly: ComVisible(false)]

namespace KeyShift
{
    internal static class Program
    {
        [STAThread]
        private static void Main(string[] args)
        {
            CommandOptions options = CommandOptions.Parse(args);

#if PREVIEW
            if (options.PreviewKind.HasValue)
            {
                PreviewState.Configure(options.PreviewKind.Value);
            }
#endif

            Application application = new Application();
            application.ShutdownMode = ShutdownMode.OnMainWindowClose;
            application.DispatcherUnhandledException += OnDispatcherUnhandledException;

            try
            {
                MainWindow window = new MainWindow(options);
                application.MainWindow = window;
                application.Run(window);
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    "KeyShift 无法启动。\n\n" + ex.Message,
                    "KeyShift",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        private static void OnDispatcherUnhandledException(
            object sender, DispatcherUnhandledExceptionEventArgs e)
        {
            MessageBox.Show(
                "KeyShift 遇到未处理的错误。\n\n" + e.Exception.Message,
                "KeyShift",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            e.Handled = true;
        }
    }

    internal sealed class CommandOptions
    {
        public LayoutKind? ApplyTarget { get; private set; }
        public LayoutKind? PreviewKind { get; private set; }
        public string ScreenshotPath { get; private set; }

        public static CommandOptions Parse(string[] args)
        {
            CommandOptions options = new CommandOptions();

            if (args == null)
            {
                return options;
            }

            for (int i = 0; i < args.Length; i++)
            {
                string argument = (args[i] ?? string.Empty).Trim();
                if (argument.Length == 0)
                {
                    continue;
                }

                string value;
                if (TryReadValue(argument, "--apply=", out value))
                {
                    LayoutKind target;
                    if (TryParseLayout(value, out target) && target != LayoutKind.Unknown)
                    {
                        options.ApplyTarget = target;
                    }
                }
#if PREVIEW
                else if (TryReadValue(argument, "--preview=", out value))
                {
                    if (string.Equals(value, "unknown", StringComparison.OrdinalIgnoreCase))
                    {
                        options.PreviewKind = LayoutKind.Unknown;
                    }
                    else
                    {
                        LayoutKind previewKind;
                        if (TryParseLayout(value, out previewKind))
                        {
                            options.PreviewKind = previewKind;
                        }
                    }
                }
                else if (TryReadValue(argument, "--screenshot=", out value))
                {
                    options.ScreenshotPath = value.Trim('"');
                }
#endif
            }

            return options;
        }

        private static bool TryReadValue(string argument, string prefix, out string value)
        {
            if (argument.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                value = argument.Substring(prefix.Length).Trim();
                return value.Length > 0;
            }

            value = null;
            return false;
        }

        private static bool TryParseLayout(string value, out LayoutKind kind)
        {
            if (string.Equals(value, "uk", StringComparison.OrdinalIgnoreCase))
            {
                kind = LayoutKind.Uk;
                return true;
            }

            if (string.Equals(value, "us", StringComparison.OrdinalIgnoreCase))
            {
                kind = LayoutKind.Us;
                return true;
            }

            kind = LayoutKind.Unknown;
            return false;
        }
    }
}
