namespace Stt.App.TextShortcuts;

public sealed record TextShortcut(
    string Name,
    string Hotkey,
    string Text,
    bool IsSecret);
