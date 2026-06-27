using System.Runtime.InteropServices;
using Blitztext.Core.Abstractions;
using Blitztext.Core.Models;
using HotkeyEventModel = Blitztext.Core.Models.HotkeyEvent;

namespace Blitztext.App.Platform;

/// <summary>
/// Global push-to-talk hotkeys via a low-level keyboard hook (WH_KEYBOARD_LL).
///
/// The macOS app used modifier-only chords (fn+Shift, fn+Ctrl, …). Windows has no usable
/// "fn" key event, so the port uses Ctrl+Alt + a trigger letter, which gives reliable
/// key-down/key-up edges for hold mode and does not collide with common shortcuts:
///
///   Ctrl+Alt+B → Blitztext            Ctrl+Alt+P → Blitztext+
///   Ctrl+Alt+L → Blitztext Lokal      Ctrl+Alt+R → Blitztext $%&! (Rage)
///   Ctrl+Alt+E → Blitztext :)         Esc        → abbrechen
///
/// The trigger key is swallowed while the chord is active so the letter is not typed.
/// </summary>
public sealed class GlobalHotkeyService : IHotkeyService
{
    private const int WH_KEYBOARD_LL = 13;
    private const int WM_KEYDOWN = 0x0100;
    private const int WM_KEYUP = 0x0101;
    private const int WM_SYSKEYDOWN = 0x0104;
    private const int WM_SYSKEYUP = 0x0105;

    private const int VK_CONTROL = 0x11;
    private const int VK_MENU = 0x12;   // Alt
    private const int VK_ESCAPE = 0x1B;

    private static readonly IReadOnlyDictionary<int, WorkflowType> ChordKeys = new Dictionary<int, WorkflowType>
    {
        [0x42] = WorkflowType.Transcription,       // B
        [0x4C] = WorkflowType.LocalTranscription,  // L
        [0x50] = WorkflowType.TextImprover,        // P
        [0x52] = WorkflowType.DampfAblassen,       // R
        [0x45] = WorkflowType.EmojiText            // E
    };

    private LowLevelKeyboardProc? _proc;
    private IntPtr _hookHandle = IntPtr.Zero;
    private WorkflowType? _activeChord;
    private int _activeKey;

    public event Action<HotkeyEvent>? HotkeyEvent;

    public void Start()
    {
        if (_hookHandle != IntPtr.Zero) return;
        _proc = HookCallback;
        _hookHandle = SetWindowsHookEx(WH_KEYBOARD_LL, _proc, GetModuleHandle(null), 0);
    }

    public void Stop()
    {
        if (_hookHandle == IntPtr.Zero) return;
        UnhookWindowsHookEx(_hookHandle);
        _hookHandle = IntPtr.Zero;
        _proc = null;
        _activeChord = null;
    }

    private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode < 0) return CallNextHookEx(_hookHandle, nCode, wParam, lParam);

        var msg = wParam.ToInt32();
        var data = Marshal.PtrToStructure<KBDLLHOOKSTRUCT>(lParam);
        int vk = (int)data.vkCode;

        bool isDown = msg is WM_KEYDOWN or WM_SYSKEYDOWN;
        bool isUp = msg is WM_KEYUP or WM_SYSKEYUP;

        if (isDown && vk == VK_ESCAPE && _activeChord is not null)
        {
            _activeChord = null;
            HotkeyEvent?.Invoke(HotkeyEventModel.Cancel);
            return CallNextHookEx(_hookHandle, nCode, wParam, lParam);
        }

        if (isDown && _activeChord is null && ChordKeys.TryGetValue(vk, out var type) && ModifiersHeld())
        {
            _activeChord = type;
            _activeKey = vk;
            HotkeyEvent?.Invoke(HotkeyEventModel.Down(type));
            return (IntPtr)1; // swallow the trigger key so the letter is not typed
        }

        if (isUp && _activeChord is not null)
        {
            // Release on trigger-key up OR when a required modifier is released.
            if (vk == _activeKey || vk == VK_CONTROL || vk == VK_MENU)
            {
                var releasedType = _activeChord.Value;
                _activeChord = null;
                HotkeyEvent?.Invoke(HotkeyEventModel.Up(releasedType));
                if (vk == _activeKey) return (IntPtr)1;
            }
        }

        return CallNextHookEx(_hookHandle, nCode, wParam, lParam);
    }

    private static bool ModifiersHeld()
    {
        bool ctrl = (GetAsyncKeyState(VK_CONTROL) & 0x8000) != 0;
        bool alt = (GetAsyncKeyState(VK_MENU) & 0x8000) != 0;
        return ctrl && alt;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct KBDLLHOOKSTRUCT
    {
        public uint vkCode;
        public uint scanCode;
        public uint flags;
        public uint time;
        public IntPtr dwExtraInfo;
    }

    private delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelKeyboardProc lpfn, IntPtr hMod, uint dwThreadId);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UnhookWindowsHookEx(IntPtr hhk);

    [DllImport("user32.dll")]
    private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern short GetAsyncKeyState(int vKey);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr GetModuleHandle(string? lpModuleName);
}
