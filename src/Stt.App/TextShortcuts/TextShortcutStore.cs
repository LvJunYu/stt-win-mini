using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Stt.App.TextShortcuts;

public sealed record LoadedTextShortcuts(
    IReadOnlyList<TextShortcut> Shortcuts,
    string? LoadErrorMessage);

// Shortcut text is encrypted with Windows DPAPI (current user scope), so the
// file is unreadable to other accounts and on other machines.
public static class TextShortcutStore
{
    private const string FileName = "whisper.text-shortcuts.json";
    private static readonly byte[] Entropy = Encoding.UTF8.GetBytes("whisper.text-shortcuts");
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };
    private static readonly JsonSerializerOptions WriteSerializerOptions = new()
    {
        WriteIndented = true
    };

    public static string FilePath { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        AppIdentity.SettingsDirectoryName,
        FileName);

    public static LoadedTextShortcuts Load()
    {
        if (!File.Exists(FilePath))
        {
            return new LoadedTextShortcuts([], null);
        }

        try
        {
            var json = File.ReadAllText(FilePath);
            var payload = JsonSerializer.Deserialize<TextShortcutsFilePayload>(json, SerializerOptions);
            var shortcuts = (payload?.Shortcuts ?? [])
                .Select(entry => new TextShortcut(
                    Name: entry.Name ?? string.Empty,
                    Hotkey: entry.Hotkey ?? string.Empty,
                    Text: Unprotect(entry.ProtectedText),
                    IsSecret: entry.IsSecret,
                    DelayMilliseconds: Math.Max(0, entry.DelayMilliseconds)))
                .ToList();

            return new LoadedTextShortcuts(shortcuts, null);
        }
        catch (Exception ex)
        {
            return new LoadedTextShortcuts(
                [],
                $"Couldn't read text shortcuts file: {FilePath}. {ex.Message}");
        }
    }

    public static void Save(IReadOnlyList<TextShortcut> shortcuts)
    {
        var directory = Path.GetDirectoryName(FilePath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var payload = new TextShortcutsFilePayload
        {
            Shortcuts = shortcuts
                .Select(shortcut => new TextShortcutEntry
                {
                    Name = shortcut.Name,
                    Hotkey = shortcut.Hotkey,
                    ProtectedText = Protect(shortcut.Text),
                    IsSecret = shortcut.IsSecret,
                    DelayMilliseconds = shortcut.DelayMilliseconds
                })
                .ToList()
        };

        var json = JsonSerializer.Serialize(payload, WriteSerializerOptions);
        File.WriteAllText(FilePath, json);
    }

    private static string Protect(string text)
    {
        var protectedBytes = ProtectedData.Protect(
            Encoding.UTF8.GetBytes(text),
            Entropy,
            DataProtectionScope.CurrentUser);

        return Convert.ToBase64String(protectedBytes);
    }

    private static string Unprotect(string? protectedText)
    {
        if (string.IsNullOrEmpty(protectedText))
        {
            return string.Empty;
        }

        var bytes = ProtectedData.Unprotect(
            Convert.FromBase64String(protectedText),
            Entropy,
            DataProtectionScope.CurrentUser);

        return Encoding.UTF8.GetString(bytes);
    }

    private sealed class TextShortcutsFilePayload
    {
        public List<TextShortcutEntry>? Shortcuts { get; init; }
    }

    private sealed class TextShortcutEntry
    {
        public string? Name { get; init; }
        public string? Hotkey { get; init; }
        public string? ProtectedText { get; init; }
        public bool IsSecret { get; init; }
        public int DelayMilliseconds { get; init; }
    }
}
