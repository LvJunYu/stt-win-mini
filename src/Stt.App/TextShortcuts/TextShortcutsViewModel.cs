using System.Collections.ObjectModel;
using System.Windows.Input;
using Stt.App.Common;
using Stt.App.Services;

namespace Stt.App.TextShortcuts;

public sealed class TextShortcutsViewModel : ObservableObject
{
    private readonly Func<string> _recordingHotkeyAccessor;

    public TextShortcutsViewModel(Func<string> recordingHotkeyAccessor)
    {
        _recordingHotkeyAccessor = recordingHotkeyAccessor;
        Shortcuts.CollectionChanged += (_, _) => OnPropertyChanged(nameof(HasShortcuts));
        AddCommand = new RelayCommand(AddShortcut);
        SaveCommand = new RelayCommand(RequestSave);
        CancelCommand = new RelayCommand(RequestClose);
    }

    public event EventHandler<TextShortcutsSaveRequestedEventArgs>? SaveRequested;
    public event EventHandler? CloseRequested;
    public event EventHandler<string>? ValidationFailed;

    public ObservableCollection<TextShortcutItemViewModel> Shortcuts { get; } = [];

    public bool HasShortcuts => Shortcuts.Count > 0;

    public string FilePath => TextShortcutStore.FilePath;

    public ICommand AddCommand { get; }
    public ICommand SaveCommand { get; }
    public ICommand CancelCommand { get; }

    public void ApplyShortcuts(IReadOnlyList<TextShortcut> shortcuts)
    {
        Shortcuts.Clear();

        foreach (var shortcut in shortcuts)
        {
            Shortcuts.Add(new TextShortcutItemViewModel(shortcut, RemoveShortcut));
        }
    }

    private void AddShortcut()
    {
        Shortcuts.Add(new TextShortcutItemViewModel(
            new TextShortcut(string.Empty, string.Empty, string.Empty, IsSecret: false),
            RemoveShortcut));
    }

    private void RemoveShortcut(TextShortcutItemViewModel item)
    {
        Shortcuts.Remove(item);
    }

    private void RequestSave()
    {
        var shortcuts = Shortcuts
            .Select(item => new TextShortcut(
                Name: item.Name.Trim(),
                Hotkey: item.Hotkey.Trim(),
                Text: item.Text,
                IsSecret: item.IsSecret))
            .ToList();

        var validationError = Validate(shortcuts);
        if (validationError is not null)
        {
            ValidationFailed?.Invoke(this, validationError);
            return;
        }

        SaveRequested?.Invoke(this, new TextShortcutsSaveRequestedEventArgs(shortcuts));
    }

    private string? Validate(IReadOnlyList<TextShortcut> shortcuts)
    {
        var usedHotkeys = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        if (HotkeyParser.TryParse(_recordingHotkeyAccessor(), out var recordingGesture, out _))
        {
            usedHotkeys[recordingGesture!.DisplayText] = "the recording hotkey";
        }

        for (var index = 0; index < shortcuts.Count; index++)
        {
            var shortcut = shortcuts[index];
            var label = string.IsNullOrWhiteSpace(shortcut.Name)
                ? $"Shortcut {index + 1}"
                : shortcut.Name;

            if (string.IsNullOrWhiteSpace(shortcut.Hotkey))
            {
                return $"{label} needs a hotkey.";
            }

            if (!HotkeyParser.TryParse(shortcut.Hotkey, out var gesture, out var parseError))
            {
                return $"{label}: {parseError}";
            }

            if (string.IsNullOrEmpty(shortcut.Text))
            {
                return $"{label} needs some text to type.";
            }

            if (usedHotkeys.TryGetValue(gesture!.DisplayText, out var owner))
            {
                return $"{label}: {gesture.DisplayText} is already used by {owner}.";
            }

            usedHotkeys[gesture.DisplayText] = label;
        }

        return null;
    }

    private void RequestClose()
    {
        CloseRequested?.Invoke(this, EventArgs.Empty);
    }
}

public sealed class TextShortcutItemViewModel : ObservableObject
{
    private string _hotkey;
    private bool _isSecret;
    private string _name;
    private string _text;

    public TextShortcutItemViewModel(
        TextShortcut shortcut,
        Action<TextShortcutItemViewModel> remove)
    {
        _name = shortcut.Name;
        _hotkey = shortcut.Hotkey;
        _text = shortcut.Text;
        _isSecret = shortcut.IsSecret;
        RemoveCommand = new RelayCommand(() => remove(this));
    }

    public string Name
    {
        get => _name;
        set => SetProperty(ref _name, value);
    }

    public string Hotkey
    {
        get => _hotkey;
        set => SetProperty(ref _hotkey, value);
    }

    public string Text
    {
        get => _text;
        set => SetProperty(ref _text, value);
    }

    public bool IsSecret
    {
        get => _isSecret;
        set => SetProperty(ref _isSecret, value);
    }

    public ICommand RemoveCommand { get; }
}

public sealed class TextShortcutsSaveRequestedEventArgs : EventArgs
{
    public TextShortcutsSaveRequestedEventArgs(IReadOnlyList<TextShortcut> shortcuts)
    {
        Shortcuts = shortcuts;
    }

    public IReadOnlyList<TextShortcut> Shortcuts { get; }
}
