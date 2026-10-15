using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using Stt.App.Services;
using FormsKeys = System.Windows.Forms.Keys;

namespace Stt.App.TextShortcuts;

public partial class TextShortcutsWindow : Window
{
    public TextShortcutsWindow()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    public bool AllowClose { get; set; }

    protected override void OnClosing(CancelEventArgs e)
    {
        if (!AllowClose)
        {
            e.Cancel = true;
            Hide();
            return;
        }

        base.OnClosing(e);
    }

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (e.OldValue is TextShortcutsViewModel oldViewModel)
        {
            oldViewModel.CloseRequested -= OnCloseRequested;
            oldViewModel.ValidationFailed -= OnValidationFailed;
        }

        if (e.NewValue is TextShortcutsViewModel newViewModel)
        {
            newViewModel.CloseRequested += OnCloseRequested;
            newViewModel.ValidationFailed += OnValidationFailed;
        }
    }

    private void OnCloseRequested(object? sender, EventArgs e)
    {
        Hide();
    }

    private void OnValidationFailed(object? sender, string message)
    {
        System.Windows.MessageBox.Show(
            message,
            $"{AppIdentity.DisplayName} Text Shortcuts",
            MessageBoxButton.OK,
            MessageBoxImage.Warning);
    }

    private void HotkeyTextBox_OnPreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (sender is not FrameworkElement { DataContext: TextShortcutItemViewModel item })
        {
            return;
        }

        var key = e.Key == Key.System ? e.SystemKey : e.Key;

        if (key == Key.Tab)
        {
            return;
        }

        e.Handled = true;

        if (key is Key.Back or Key.Delete)
        {
            item.Hotkey = string.Empty;
            return;
        }

        if (IsModifierOnlyKey(key))
        {
            return;
        }

        var formsKey = (FormsKeys)KeyInterop.VirtualKeyFromKey(key);
        var modifierFlags = HotkeyParser.ToModifierFlags(Keyboard.Modifiers);
        item.Hotkey = HotkeyParser.FormatDisplayText(modifierFlags, formsKey);
    }

    private void HotkeyTextBox_OnPreviewTextInput(object sender, TextCompositionEventArgs e)
    {
        e.Handled = true;
    }

    // PasswordBox.Password isn't bindable, so it is synced with the item by hand.
    private void SecretPasswordBox_OnLoaded(object sender, RoutedEventArgs e)
    {
        SyncPasswordBoxFromItem(sender);
    }

    private void SecretPasswordBox_OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        SyncPasswordBoxFromItem(sender);
    }

    private void SecretPasswordBox_OnIsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (e.NewValue is true)
        {
            SyncPasswordBoxFromItem(sender);
        }
    }

    private void SecretPasswordBox_OnPasswordChanged(object sender, RoutedEventArgs e)
    {
        if (sender is PasswordBox { DataContext: TextShortcutItemViewModel item } passwordBox)
        {
            item.Text = passwordBox.Password;
        }
    }

    private static void SyncPasswordBoxFromItem(object sender)
    {
        if (sender is PasswordBox { DataContext: TextShortcutItemViewModel item } passwordBox
            && passwordBox.Password != item.Text)
        {
            passwordBox.Password = item.Text;
        }
    }

    private void DigitsTextBox_OnPreviewTextInput(object sender, TextCompositionEventArgs e)
    {
        e.Handled = !e.Text.All(char.IsAsciiDigit);
    }

    private void DigitsTextBox_OnPasting(object sender, DataObjectPastingEventArgs e)
    {
        var pastedText = e.DataObject.GetData(System.Windows.DataFormats.Text) as string;
        if (string.IsNullOrEmpty(pastedText) || !pastedText.All(char.IsAsciiDigit))
        {
            e.CancelCommand();
        }
    }

    private static bool IsModifierOnlyKey(Key key)
    {
        return key is Key.LeftCtrl
            or Key.RightCtrl
            or Key.LeftAlt
            or Key.RightAlt
            or Key.LeftShift
            or Key.RightShift
            or Key.LWin
            or Key.RWin;
    }
}
