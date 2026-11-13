using System;

namespace KeyShift
{
    internal static class ServiceTests
    {
        private static int _failures;

        private static int Main()
        {
            LayoutKind kind;
            kind = KeyboardLayoutService.Classify("KBDUS.DLL");
            Check("classify US", kind == LayoutKind.Us);
            kind = KeyboardLayoutService.Classify("kbduk.dll");
            Check("classify UK (case-insensitive)", kind == LayoutKind.Uk);
            kind = KeyboardLayoutService.Classify("KBDFR.DLL");
            Check("classify unknown", kind == LayoutKind.Unknown);

            CheckEqual(
                "US -> UK English label",
                KeyboardLayoutService.BuildLayoutText("Chinese (Simplified) - US Keyboard", LayoutKind.Uk),
                "Chinese (Simplified) - UK Keyboard");
            CheckEqual(
                "UK -> US English label",
                KeyboardLayoutService.BuildLayoutText("Chinese (Simplified) - UK Keyboard", LayoutKind.Us),
                "Chinese (Simplified) - US Keyboard");
            CheckEqual(
                "US -> UK Chinese label",
                KeyboardLayoutService.BuildLayoutText("中文(简体) - 美式键盘", LayoutKind.Uk),
                "中文(简体) - 英式键盘");
            CheckEqual(
                "custom label is preserved",
                KeyboardLayoutService.BuildLayoutText("My Custom Layout", LayoutKind.Uk),
                "My Custom Layout");
            CheckEqual(
                "empty label becomes target default",
                KeyboardLayoutService.BuildLayoutText("", LayoutKind.Uk),
                "Chinese (Simplified) - UK Keyboard");
            CheckEqual(
                "already target label stays stable",
                KeyboardLayoutService.BuildLayoutText("Chinese (Simplified) - UK Keyboard", LayoutKind.Uk),
                "Chinese (Simplified) - UK Keyboard");

            LayoutState actual = KeyboardLayoutService.Read();
            Console.WriteLine("Registry read: Kind={0}, LayoutFile={1}, LayoutText={2}",
                actual.Kind, actual.LayoutFile, actual.LayoutText);
            Check("registry read recognized", actual.IsKnown);

            Console.WriteLine("Elevated={0}", ElevationHelper.IsElevated);
            Console.WriteLine(_failures == 0 ? "ALL TESTS PASSED" : _failures + " TEST(S) FAILED");
            return _failures == 0 ? 0 : 1;
        }

        private static void Check(string name, bool condition)
        {
            Console.WriteLine("[{0}] {1}", condition ? "PASS" : "FAIL", name);
            if (!condition)
            {
                _failures++;
            }
        }

        private static void CheckEqual(string name, string actual, string expected)
        {
            bool ok = string.Equals(actual, expected, StringComparison.Ordinal);
            Console.WriteLine("[{0}] {1}", ok ? "PASS" : "FAIL", name);
            if (!ok)
            {
                Console.WriteLine("  expected: {0}", expected);
                Console.WriteLine("  actual:   {0}", actual);
                _failures++;
            }
        }
    }
}
