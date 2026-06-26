using Blitztext.Core.Models;

namespace Blitztext.Core.Abstractions;

/// <summary>
/// Global hotkey listener. The Windows implementation installs a low-level keyboard hook
/// (WH_KEYBOARD_LL) and raises <see cref="HotkeyEvent"/>s for the five workflow chords,
/// plus a cancel event on Escape. Mirrors the macOS <c>HotkeyService</c>.
/// </summary>
public interface IHotkeyService
{
    event Action<HotkeyEvent>? HotkeyEvent;

    void Start();
    void Stop();
}
