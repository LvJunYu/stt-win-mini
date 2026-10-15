using Stt.App.Services;
using Stt.Core.Diagnostics;

namespace Stt.App.TextShortcuts;

public sealed class TextShortcutHotkeyHost : IDisposable
{
    private readonly List<GlobalHotkeyService> _hotkeyServices = [];

    // Re-registers every shortcut. Returns one message per shortcut that
    // couldn't be registered; the others stay active.
    public IReadOnlyList<string> Apply(IReadOnlyList<TextShortcut> shortcuts)
    {
        DisposeHotkeys();

        var errors = new List<string>();

        foreach (var shortcut in shortcuts)
        {
            var label = GetDisplayName(shortcut);

            if (!HotkeyParser.TryParse(shortcut.Hotkey, out var gesture, out var parseError))
            {
                errors.Add($"{label}: {parseError}");
                continue;
            }

            try
            {
                _hotkeyServices.Add(new GlobalHotkeyService(gesture!, () => TypeShortcutAsync(shortcut)));
            }
            catch (Exception ex)
            {
                errors.Add($"{label}: {ex.Message}");
            }
        }

        WhisperTrace.Log(
            "TextShortcut",
            $"Registered {_hotkeyServices.Count} of {shortcuts.Count} text shortcuts.");

        return errors;
    }

    public void Dispose()
    {
        DisposeHotkeys();
    }

    public static string GetDisplayName(TextShortcut shortcut)
    {
        return string.IsNullOrWhiteSpace(shortcut.Name)
            ? shortcut.Hotkey
            : shortcut.Name;
    }

    private static async Task TypeShortcutAsync(TextShortcut shortcut)
    {
        if (shortcut.DelayMilliseconds > 0)
        {
            await Task.Delay(shortcut.DelayMilliseconds);
        }

        var typed = await TextShortcutTyper.TypeAsync(shortcut.Text);

        // Never log the text itself; it may be a password.
        WhisperTrace.Log(
            "TextShortcut",
            typed
                ? $"Typed text shortcut '{GetDisplayName(shortcut)}'."
                : $"Couldn't type text shortcut '{GetDisplayName(shortcut)}'.");
    }

    private void DisposeHotkeys()
    {
        foreach (var hotkeyService in _hotkeyServices)
        {
            hotkeyService.Dispose();
        }

        _hotkeyServices.Clear();
    }
}
