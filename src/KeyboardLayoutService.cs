using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Security.Principal;
using Microsoft.Win32;

namespace KeyShift
{
    internal enum LayoutKind
    {
        Unknown = 0,
        Us = 1,
        Uk = 2
    }

    internal sealed class LayoutState
    {
        public LayoutState(LayoutKind kind, string layoutFile, string layoutText)
        {
            Kind = kind;
            LayoutFile = string.IsNullOrEmpty(layoutFile) ? string.Empty : layoutFile;
            LayoutText = layoutText ?? string.Empty;
        }

        public LayoutKind Kind { get; private set; }
        public string LayoutFile { get; private set; }
        public string LayoutText { get; private set; }

        public bool IsKnown
        {
            get { return Kind != LayoutKind.Unknown; }
        }

        public string Badge
        {
            get
            {
                if (Kind == LayoutKind.Uk) return "UK";
                if (Kind == LayoutKind.Us) return "US";
                return "?";
            }
        }

        public string DisplayName
        {
            get
            {
                if (Kind == LayoutKind.Uk) return "英式键盘";
                if (Kind == LayoutKind.Us) return "美式键盘";
                return "未知布局";
            }
        }

        public string CountryName
        {
            get
            {
                if (Kind == LayoutKind.Uk) return "United Kingdom";
                if (Kind == LayoutKind.Us) return "United States";
                return "Unrecognized layout";
            }
        }

        public string FileName
        {
            get
            {
                if (!string.IsNullOrEmpty(LayoutFile)) return LayoutFile;
                if (Kind == LayoutKind.Uk) return KeyboardLayoutService.UkFileName;
                if (Kind == LayoutKind.Us) return KeyboardLayoutService.UsFileName;
                return "—";
            }
        }
    }

    internal static class KeyboardLayoutService
    {
        internal const string SubKeyPath = @"SYSTEM\CurrentControlSet\Control\Keyboard Layouts\00000804";
        internal const string FullRegistryPath = @"HKEY_LOCAL_MACHINE\" + SubKeyPath;
        internal const string UsFileName = "KBDUS.DLL";
        internal const string UkFileName = "KBDUK.DLL";
        internal const string DefaultUsText = "Chinese (Simplified) - US Keyboard";

        public static LayoutState Read()
        {
#if PREVIEW
            if (PreviewState.Enabled)
            {
                return PreviewState.Read();
            }
#endif

            try
            {
                using (RegistryKey baseKey = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, PreferredRegistryView))
                using (RegistryKey key = baseKey.OpenSubKey(SubKeyPath, false))
                {
                    if (key == null)
                    {
                        return new LayoutState(LayoutKind.Unknown, string.Empty, string.Empty);
                    }

                    string layoutFile = Convert.ToString(
                        key.GetValue("Layout File", string.Empty, RegistryValueOptions.DoNotExpandEnvironmentNames));
                    string layoutText = Convert.ToString(
                        key.GetValue("Layout Text", string.Empty, RegistryValueOptions.DoNotExpandEnvironmentNames));

                    LayoutKind kind = Classify(layoutFile);
                    return new LayoutState(kind, layoutFile, layoutText);
                }
            }
            catch (UnauthorizedAccessException ex)
            {
                throw new UnauthorizedAccessException("无法读取键盘布局注册表项。", ex);
            }
            catch (System.Security.SecurityException ex)
            {
                throw new UnauthorizedAccessException("无法读取键盘布局注册表项。", ex);
            }
        }

        public static void Apply(LayoutKind target)
        {
            if (target != LayoutKind.Us && target != LayoutKind.Uk)
            {
                throw new ArgumentOutOfRangeException("target");
            }

            string targetFile = target == LayoutKind.Uk ? UkFileName : UsFileName;
            string systemDirectory = Environment.GetFolderPath(Environment.SpecialFolder.System);
            string dllPath = Path.Combine(systemDirectory, targetFile);

            if (!File.Exists(dllPath))
            {
                throw new FileNotFoundException(
                    "系统中找不到 " + targetFile + "，因此无法切换到该布局。", dllPath);
            }

#if PREVIEW
            if (PreviewState.Enabled)
            {
                PreviewState.Apply(target);
                return;
            }
#endif

            using (RegistryKey baseKey = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, PreferredRegistryView))
            using (RegistryKey key = baseKey.OpenSubKey(SubKeyPath, true))
            {
                if (key == null)
                {
                    throw new InvalidOperationException(
                        "找不到注册表项：HKEY_LOCAL_MACHINE\\" + SubKeyPath);
                }

                object oldFileValue = key.GetValue(
                    "Layout File", null, RegistryValueOptions.DoNotExpandEnvironmentNames);
                object oldTextValue = key.GetValue(
                    "Layout Text", null, RegistryValueOptions.DoNotExpandEnvironmentNames);

                string oldFile = oldFileValue == null ? string.Empty : Convert.ToString(oldFileValue);
                string oldText = oldTextValue == null ? string.Empty : Convert.ToString(oldTextValue);
                string newText = BuildLayoutText(oldText, target);

                bool fileChanged = !string.Equals(oldFile, targetFile, StringComparison.OrdinalIgnoreCase);
                bool textChanged = !string.Equals(oldText, newText, StringComparison.Ordinal);

                try
                {
                    if (fileChanged)
                    {
                        key.SetValue("Layout File", targetFile, RegistryValueKind.String);
                    }

                    if (textChanged)
                    {
                        key.SetValue("Layout Text", newText, RegistryValueKind.String);
                    }

                    key.Flush();
                }
                catch
                {
                    // Best-effort rollback so a partially written layout is never left behind.
                    try
                    {
                        if (fileChanged)
                        {
                            RestoreValue(key, "Layout File", oldFileValue);
                        }

                        if (textChanged)
                        {
                            RestoreValue(key, "Layout Text", oldTextValue);
                        }

                        key.Flush();
                    }
                    catch
                    {
                        // Preserve the original exception.
                    }

                    throw;
                }
            }
        }

        private static void RestoreValue(RegistryKey key, string name, object originalValue)
        {
            if (originalValue == null)
            {
                key.DeleteValue(name, false);
            }
            else
            {
                key.SetValue(name, originalValue, RegistryValueKind.String);
            }
        }

        internal static string BuildLayoutText(string current, LayoutKind target)
        {
            string targetEnglish = target == LayoutKind.Uk ? "UK Keyboard" : "US Keyboard";
            string otherEnglish = target == LayoutKind.Uk ? "US Keyboard" : "UK Keyboard";
            string targetChinese = target == LayoutKind.Uk ? "英式键盘" : "美式键盘";
            string otherChinese = target == LayoutKind.Uk ? "美式键盘" : "英式键盘";

            if (string.IsNullOrWhiteSpace(current))
            {
                return target == LayoutKind.Uk
                    ? "Chinese (Simplified) - UK Keyboard"
                    : DefaultUsText;
            }

            if (current.IndexOf(otherEnglish, StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return ReplaceIgnoreCase(current, otherEnglish, targetEnglish);
            }

            if (current.IndexOf(otherChinese, StringComparison.Ordinal) >= 0)
            {
                return current.Replace(otherChinese, targetChinese);
            }

            // A localized or custom label that we do not recognize is intentionally preserved.
            return current;
        }

        private static string ReplaceIgnoreCase(string input, string oldValue, string newValue)
        {
            int index = input.IndexOf(oldValue, StringComparison.OrdinalIgnoreCase);
            if (index < 0)
            {
                return input;
            }

            return input.Substring(0, index) + newValue + input.Substring(index + oldValue.Length);
        }

        internal static LayoutKind Classify(string layoutFile)
        {
            if (string.IsNullOrWhiteSpace(layoutFile))
            {
                return LayoutKind.Unknown;
            }

            if (string.Equals(layoutFile.Trim(), UkFileName, StringComparison.OrdinalIgnoreCase))
            {
                return LayoutKind.Uk;
            }

            if (string.Equals(layoutFile.Trim(), UsFileName, StringComparison.OrdinalIgnoreCase))
            {
                return LayoutKind.Us;
            }

            return LayoutKind.Unknown;
        }

        private static RegistryView PreferredRegistryView
        {
            get
            {
                return Environment.Is64BitOperatingSystem ? RegistryView.Registry64 : RegistryView.Registry32;
            }
        }
    }

    internal sealed class ElevationResult
    {
        public bool Started { get; set; }
        public bool Canceled { get; set; }
        public string ErrorMessage { get; set; }
    }

    internal static class ElevationHelper
    {
        public static bool IsElevated
        {
            get
            {
                try
                {
                    using (WindowsIdentity identity = WindowsIdentity.GetCurrent())
                    {
                        WindowsPrincipal principal = new WindowsPrincipal(identity);
                        return principal.IsInRole(WindowsBuiltInRole.Administrator);
                    }
                }
                catch
                {
                    return false;
                }
            }
        }

        public static ElevationResult LaunchElevated(LayoutKind target)
        {
            ElevationResult result = new ElevationResult();

            try
            {
                string executablePath = Process.GetCurrentProcess().MainModule.FileName;
                ProcessStartInfo startInfo = new ProcessStartInfo();
                startInfo.FileName = executablePath;
                startInfo.Arguments = "--apply=" + target.ToString().ToLowerInvariant();
                startInfo.WorkingDirectory = AppDomain.CurrentDomain.BaseDirectory;
                startInfo.UseShellExecute = true;
                startInfo.Verb = "runas";
                Process.Start(startInfo);
                result.Started = true;
            }
            catch (Win32Exception ex)
            {
                if (ex.NativeErrorCode == 1223)
                {
                    result.Canceled = true;
                }
                else
                {
                    result.ErrorMessage = ex.Message;
                }
            }
            catch (Exception ex)
            {
                result.ErrorMessage = ex.Message;
            }

            return result;
        }
    }

#if PREVIEW
    internal static class PreviewState
    {
        public static bool Enabled;
        public static LayoutKind Kind = LayoutKind.Uk;

        public static void Configure(LayoutKind kind)
        {
            Enabled = true;
            Kind = kind;
        }

        public static LayoutState Read()
        {
            string file = Kind == LayoutKind.Us ? KeyboardLayoutService.UsFileName
                : Kind == LayoutKind.Uk ? KeyboardLayoutService.UkFileName
                : "KBD??.DLL";
            string text = Kind == LayoutKind.Uk
                ? "Chinese (Simplified) - UK Keyboard"
                : "Chinese (Simplified) - US Keyboard";

            return new LayoutState(Kind, file, text);
        }

        public static void Apply(LayoutKind target)
        {
            Kind = target;
        }
    }
#endif
}
