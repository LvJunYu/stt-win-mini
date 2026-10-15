using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows.Input;
using Stt.App.Common;
using Stt.App.Services;

namespace Stt.App.TextShortcuts;

public sealed class TextShortcutsViewModel : ObservableObject
{
    private const int MaxDelayMilliseconds = 60_000;
    private readonly Func<string> _recordingHotkeyAccessor;
    private readonly RelayCommand _removeCommand;
    private TextShortcutItemViewModel? _selectedShortcut;

    public TextShortcutsViewModel(Func<string> recordingHotkeyAccessor)
    {
        _recordingHotkeyAccessor = recordingHotkeyAccessor;
        _removeCommand = new RelayCommand(RemoveSelectedShortcut, () => SelectedShortcut is not null);
        Shortcuts.CollectionChanged += (_, _) => OnPropertyChanged(nameof(HasShortcuts));
        AddCommand = new RelayCommand(AddShortcut);
        SaveCommand = new RelayCommand(RequestSave);
        CancelCommand = new RelayCommand(RequestClose);
    }

    public event EventHandler<TextShortcutsSaveRequestedEventArgs>? SaveRequested;
    public event EventHandler? CloseRequested;
    public event EventHandler<string>? ValidationFailed;

    public ObservableCollection<TextShortcutItemViewModel> Shortcuts { get; } = [];

    public TextShortcutItemViewModel? SelectedShortcut
    {
        get => _selectedShortcut;
        set
        {
            if (!SetProperty(ref _selectedShortcut, value))
            {
                return;
            }

            OnPropertyChanged(nameof(HasSelection));
            _removeCommand.NotifyCanExecuteChanged();
        }
    }

    public bool HasShortcuts => Shortcuts.Count > 0;

    public bool HasSelection => SelectedShortcut is not null;

    public string FilePath => TextShortcutStore.FilePath;

    public ICommand AddCommand { get; }
    public ICommand RemoveCommand => _removeCommand;
    public ICommand SaveCommand { get; }
    public ICommand CancelCommand { get; }

    public void ApplyShortcuts(IReadOnlyList<TextShortcut> shortcuts)
    {
        Shortcuts.Clear();

        foreach (var shortcut in shortcuts)
        {
            Shortcuts.Add(new TextShortcutItemViewModel(shortcut));
        }

        SelectedShortcut = Shortcuts.FirstOrDefault();
    }

    private void AddShortcut()
    {
        var item = new TextShortcutItemViewModel(new TextShortcut(
            Name: string.Empty,
            Hotkey: string.Empty,
            Text: string.Empty,
            IsSecret: false,
            DelayMilliseconds: 0));

        Shortcuts.Add(item);
        SelectedShortcut = item;
    }

    private void RemoveSelectedShortcut()
    {
        if (SelectedShortcut is null)
        {
            return;
        }

        var index = Shortcuts.IndexOf(SelectedShortcut);
        Shortcuts.RemoveAt(index);
        SelectedShortcut = Shortcuts.Count == 0
            ? null
            : Shortcuts[Math.Min(index, Shortcuts.Count - 1)];
    }

    private void RequestSave()
    {
        var shortcuts = new List<TextShortcut>(Shortcuts.Count);
        var usedHotkeys = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        if (HotkeyParser.TryParse(_recordingHotkeyAccessor(), out var recordingGesture, out _))
        {
            usedHotkeys[recordingGesture!.DisplayText] = "the recording hotkey";
        }

        foreach (var item in Shortcuts)
        {
            var error = Validate(item, usedHotkeys, out var shortcut);
            if (error is not null)
            {
                SelectedShortcut = item;
                ValidationFailed?.Invoke(this, $"{item.DisplayName}: {error}");
                return;
            }

            shortcuts.Add(shortcut!);
        }

        SaveRequested?.Invoke(this, new TextShortcutsSaveRequestedEventArgs(shortcuts));
    }

    private static string? Validate(
        TextShortcutItemViewModel item,
        Dictionary<string, string> usedHotkeys,
        out TextShortcut? shortcut)
    {
        shortcut = null;

        if (string.IsNullOrWhiteSpace(item.Hotkey))
        {
            return "needs a hotkey.";
        }

        if (!HotkeyParser.TryParse(item.Hotkey, out var gesture, out var parseError))
        {
            return parseError;
        }

        if (usedHotkeys.TryGetValue(gesture!.DisplayText, out var owner))
        {
            return $"{gesture.DisplayText} is already used by {owner}.";
        }

        if (string.IsNullOrEmpty(item.Text))
        {
            return "needs some text to type.";
        }

        var delayText = item.DelayMillisecondsText.Trim();
        var delayMilliseconds = 0;
        if (delayText.Length > 0
            && (!int.TryParse(delayText, NumberStyles.None, CultureInfo.InvariantCulture, out delayMilliseconds)
                || delayMilliseconds > MaxDelayMilliseconds))
        {
            return $"delay must be a whole number from 0 to {MaxDelayMilliseconds} ms.";
        }

        usedHotkeys[gesture.DisplayText] = item.DisplayName;
        shortcut = new TextShortcut(
            Name: item.Name.Trim(),
            Hotkey: gesture.DisplayText,
            Text: item.Text,
            IsSecret: item.IsSecret,
            DelayMilliseconds: delayMilliseconds);

        return null;
    }

    private void RequestClose()
    {
        CloseRequested?.Invoke(this, EventArgs.Empty);
    }
}

public sealed class TextShortcutItemViewModel : ObservableObject
{
    private string _delayMillisecondsText;
    private string _hotkey;
    private bool _isSecret;
    private string _name;
    private string _text;

    public TextShortcutItemViewModel(TextShortcut shortcut)
    {
        _name = shortcut.Name;
        _hotkey = shortcut.Hotkey;
        _text = shortcut.Text;
        _isSecret = shortcut.IsSecret;
        _delayMillisecondsText = shortcut.DelayMilliseconds.ToString(CultureInfo.InvariantCulture);
    }

    public string Name
    {
        get => _name;
        set
        {
            if (SetProperty(ref _name, value))
            {
                OnPropertyChanged(nameof(DisplayName));
            }
        }
    }

    public string Hotkey
    {
        get => _hotkey;
        set
        {
            if (SetProperty(ref _hotkey, value))
            {
                OnPropertyChanged(nameof(HotkeyDisplayText));
            }
        }
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

    public string DelayMillisecondsText
    {
        get => _delayMillisecondsText;
        set => SetProperty(ref _delayMillisecondsText, value);
    }

    public string DisplayName => string.IsNullOrWhiteSpace(Name) ? "Untitled" : Name.Trim();

    public string HotkeyDisplayText => string.IsNullOrWhiteSpace(Hotkey) ? "No hotkey" : Hotkey;
}

public sealed class TextShortcutsSaveRequestedEventArgs : EventArgs
{
    public TextShortcutsSaveRequestedEventArgs(IReadOnlyList<TextShortcut> shortcuts)
    {
        Shortcuts = shortcuts;
    }

    public IReadOnlyList<TextShortcut> Shortcuts { get; }
}
