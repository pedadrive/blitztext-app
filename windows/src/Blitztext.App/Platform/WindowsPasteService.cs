using System.Runtime.InteropServices;
using System.Windows;
using Blitztext.Core.Abstractions;

namespace Blitztext.App.Platform;

/// <summary>
/// Clipboard + auto-paste for Windows. Mirrors the macOS AppState paste logic: snapshot the
/// foreground window, copy the result to the clipboard (kept as a fallback), restore focus,
/// then synthesize Ctrl+V via SendInput. Clipboard and input synthesis are marshalled to the
/// WPF UI (STA) thread.
/// </summary>
public sealed class WindowsPasteService : IPasteService
{
    private sealed class WindowTarget : IPasteTarget
    {
        public IntPtr Hwnd { get; init; }
    }

    public IPasteTarget? CaptureForegroundTarget()
    {
        var hwnd = GetForegroundWindow();
        return hwnd == IntPtr.Zero ? null : new WindowTarget { Hwnd = hwnd };
    }

    public void CopyToClipboard(string text) => Invoke(() => SetClipboard(text));

    public bool PasteAtCursor(string text, IPasteTarget? target)
    {
        return Invoke(() =>
        {
            SetClipboard(text);

            if (target is WindowTarget { Hwnd: var hwnd } && hwnd != IntPtr.Zero && hwnd != GetForegroundWindow())
            {
                SetForegroundWindow(hwnd);
                // Give the target a moment to receive focus before sending keys.
                Thread.Sleep(40);
            }

            SendCtrlV();
            return true;
        });
    }

    private static T Invoke<T>(Func<T> action)
    {
        var dispatcher = Application.Current?.Dispatcher;
        return dispatcher is null || dispatcher.CheckAccess()
            ? action()
            : dispatcher.Invoke(action);
    }

    private static void Invoke(Action action) => Invoke(() => { action(); return true; });

    private static void SetClipboard(string text)
    {
        try
        {
            Clipboard.SetText(text);
        }
        catch (Exception)
        {
            // Clipboard can be transiently locked by another process; retry once.
            Thread.Sleep(30);
            Clipboard.SetText(text);
        }
    }

    private static void SendCtrlV()
    {
        var inputs = new[]
        {
            KeyInput(VK_CONTROL, false),
            KeyInput(0x56, false), // V down
            KeyInput(0x56, true),  // V up
            KeyInput(VK_CONTROL, true)
        };
        SendInput((uint)inputs.Length, inputs, Marshal.SizeOf<INPUT>());
    }

    private static INPUT KeyInput(ushort vk, bool keyUp) => new()
    {
        type = INPUT_KEYBOARD,
        U = new InputUnion
        {
            ki = new KEYBDINPUT
            {
                wVk = vk,
                dwFlags = keyUp ? KEYEVENTF_KEYUP : 0
            }
        }
    };

    // --- Win32 interop ---

    private const ushort VK_CONTROL = 0x11;
    private const uint INPUT_KEYBOARD = 1;
    private const uint KEYEVENTF_KEYUP = 0x0002;

    [StructLayout(LayoutKind.Sequential)]
    private struct INPUT
    {
        public uint type;
        public InputUnion U;
    }

    [StructLayout(LayoutKind.Explicit)]
    private struct InputUnion
    {
        [FieldOffset(0)] public KEYBDINPUT ki;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct KEYBDINPUT
    {
        public ushort wVk;
        public ushort wScan;
        public uint dwFlags;
        public uint time;
        public IntPtr dwExtraInfo;
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetForegroundWindow(IntPtr hWnd);
}
