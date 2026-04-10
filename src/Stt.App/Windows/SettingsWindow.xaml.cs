using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using Stt.App.Services;
using Stt.App.ViewModels;
using FormsKeys = System.Windows.Forms.Keys;
using WpfTextBox = System.Windows.Controls.TextBox;

namespace Stt.App.Windows;

public partial class SettingsWindow : Window
{
    public SettingsWindow()
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
        if (e.OldValue is SettingsViewModel oldViewModel)
        {
            oldViewModel.CloseRequested -= OnCloseRequested;
            oldViewModel.ValidationFailed -= OnValidationFailed;
        }

        if (e.NewValue is SettingsViewModel newViewModel)
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
            $"{AppIdentity.DisplayName} Settings",
            MessageBoxButton.OK,
            MessageBoxImage.Warning);
    }

    private void HotkeyTextBox_OnPreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (DataContext is not SettingsViewModel viewModel)
        {
            return;
        }

        var key = e.Key == Key.System ? e.SystemKey : e.Key;

        if (key == Key.Tab)
        {
            return;
        }

        if (key is Key.Back or Key.Delete)
        {
            viewModel.ToggleRecordingHotkey = string.Empty;
            e.Handled = true;
            return;
        }

        if (IsModifierOnlyKey(key))
        {
            e.Handled = true;
            return;
        }

        var modifiers = Keyboard.Modifiers;
        var formsKey = (FormsKeys)KeyInterop.VirtualKeyFromKey(key);
        var modifierFlags = HotkeyParser.ToModifierFlags(modifiers);

        viewModel.ToggleRecordingHotkey = HotkeyParser.FormatDisplayText(modifierFlags, formsKey);
        e.Handled = true;
    }

    private void HotkeyTextBox_OnPreviewTextInput(object sender, TextCompositionEventArgs e)
    {
        e.Handled = true;
    }

    private void ClearHotkeyButton_OnClick(object sender, RoutedEventArgs e)
    {
        if (DataContext is SettingsViewModel viewModel)
        {
            viewModel.ToggleRecordingHotkey = string.Empty;
        }
    }

    private void WholeNumberTextBox_OnPreviewTextInput(object sender, TextCompositionEventArgs e)
    {
        foreach (var character in e.Text)
        {
            if (!char.IsDigit(character))
            {
                e.Handled = true;
                return;
            }
        }
    }

    private void WholeNumberTextBox_OnPasting(object sender, DataObjectPastingEventArgs e)
    {
        if (!e.DataObject.GetDataPresent(System.Windows.DataFormats.Text))
        {
            e.CancelCommand();
            return;
        }

        var pastedText = e.DataObject.GetData(System.Windows.DataFormats.Text) as string ?? string.Empty;
        foreach (var character in pastedText)
        {
            if (!char.IsDigit(character))
            {
                e.CancelCommand();
                return;
            }
        }
    }

    private void WholeNumberTextBox_OnTextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
    {
        if (sender is not WpfTextBox textBox)
        {
            return;
        }

        var originalText = textBox.Text ?? string.Empty;
        var sanitizedText = new string(Array.FindAll(originalText.ToCharArray(), char.IsDigit));
        if (sanitizedText == originalText)
        {
            return;
        }

        var safeCaretIndex = Math.Min(textBox.CaretIndex, originalText.Length);
        var digitsBeforeCaret = Array.FindAll(originalText[..safeCaretIndex].ToCharArray(), char.IsDigit).Length;

        textBox.Text = sanitizedText;
        textBox.CaretIndex = Math.Min(digitsBeforeCaret, sanitizedText.Length);
    }

    private void DecimalNumberTextBox_OnPreviewTextInput(object sender, TextCompositionEventArgs e)
    {
        if (sender is not WpfTextBox textBox)
        {
            return;
        }

        var proposedText = GetProposedText(textBox, e.Text);
        e.Handled = proposedText != SanitizeDecimalText(proposedText);
    }

    private void DecimalNumberTextBox_OnPasting(object sender, DataObjectPastingEventArgs e)
    {
        if (sender is not WpfTextBox textBox)
        {
            e.CancelCommand();
            return;
        }

        if (!e.DataObject.GetDataPresent(System.Windows.DataFormats.Text))
        {
            e.CancelCommand();
            return;
        }

        var pastedText = e.DataObject.GetData(System.Windows.DataFormats.Text) as string ?? string.Empty;
        var proposedText = GetProposedText(textBox, pastedText);
        if (proposedText != SanitizeDecimalText(proposedText))
        {
            e.CancelCommand();
        }
    }

    private void DecimalNumberTextBox_OnTextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
    {
        if (sender is not WpfTextBox textBox)
        {
            return;
        }

        var originalText = textBox.Text ?? string.Empty;
        var sanitizedText = SanitizeDecimalText(originalText);
        if (sanitizedText == originalText)
        {
            return;
        }

        var safeCaretIndex = Math.Min(textBox.CaretIndex, originalText.Length);
        var sanitizedBeforeCaret = SanitizeDecimalText(originalText[..safeCaretIndex]);

        textBox.Text = sanitizedText;
        textBox.CaretIndex = Math.Min(sanitizedBeforeCaret.Length, sanitizedText.Length);
    }

    private static string GetProposedText(WpfTextBox textBox, string insertedText)
    {
        var originalText = textBox.Text ?? string.Empty;
        var selectionStart = textBox.SelectionStart;
        var selectionLength = textBox.SelectionLength;
        return originalText.Remove(selectionStart, selectionLength).Insert(selectionStart, insertedText);
    }

    private static string SanitizeDecimalText(string value)
    {
        var builder = new System.Text.StringBuilder(value.Length);
        var decimalPointSeen = false;

        foreach (var character in value)
        {
            if (char.IsDigit(character))
            {
                builder.Append(character);
                continue;
            }

            if (character == '.' && !decimalPointSeen)
            {
                builder.Append(character);
                decimalPointSeen = true;
            }
        }

        return builder.ToString();
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
