using System.Runtime.InteropServices;
using Stt.Core.Diagnostics;

namespace Stt.App.TextShortcuts;

// Types text into the focused window with SendInput unicode events instead of
// going through the clipboard, so secrets never land in clipboard history and
// fields that block paste still accept the text.
public static class TextShortcutTyper
{
    private const uint InputKeyboard = 1;
    private const uint KeyEventKeyUp = 0x0002;
    private const uint KeyEventUnicode = 0x0004;
    private const ushort VirtualKeyReturn = 0x0D;
    private const ushort VirtualKeyTab = 0x09;
    private static readonly int[] ModifierVirtualKeys = [0x10, 0x11, 0x12, 0x5B, 0x5C];
    private static readonly TimeSpan ModifierReleaseTimeout = TimeSpan.FromSeconds(2);
    private static readonly TimeSpan ModifierPollInterval = TimeSpan.FromMilliseconds(15);

    public static async Task<bool> TypeAsync(string text)
    {
        // The hotkey's modifiers are usually still held when it fires. Typing
        // while Ctrl/Alt/Win is down would turn characters into shortcuts.
        if (!await WaitForModifierReleaseAsync())
        {
            WhisperTrace.Log("TextShortcut", "Modifier keys were still held after the timeout. Nothing was typed.");
            return false;
        }

        var inputs = BuildInputs(text);
        if (inputs.Length == 0)
        {
            return true;
        }

        var sentCount = SendInput((uint)inputs.Length, inputs, Marshal.SizeOf<Input>());
        if (sentCount != inputs.Length)
        {
            WhisperTrace.Log(
                "TextShortcut",
                $"SendInput sent {sentCount} of {inputs.Length} events. Error={Marshal.GetLastWin32Error()}.");
            return false;
        }

        return true;
    }

    private static async Task<bool> WaitForModifierReleaseAsync()
    {
        var deadline = DateTimeOffset.UtcNow + ModifierReleaseTimeout;

        while (IsAnyModifierDown())
        {
            if (DateTimeOffset.UtcNow >= deadline)
            {
                return false;
            }

            await Task.Delay(ModifierPollInterval);
        }

        return true;
    }

    private static bool IsAnyModifierDown()
    {
        return ModifierVirtualKeys.Any(virtualKey => (GetAsyncKeyState(virtualKey) & 0x8000) != 0);
    }

    private static Input[] BuildInputs(string text)
    {
        var inputs = new List<Input>(text.Length * 2);

        for (var index = 0; index < text.Length; index++)
        {
            var character = text[index];

            switch (character)
            {
                case '\r':
                    if (index + 1 < text.Length && text[index + 1] == '\n')
                    {
                        continue;
                    }

                    AddVirtualKeyPress(inputs, VirtualKeyReturn);
                    continue;
                case '\n':
                    AddVirtualKeyPress(inputs, VirtualKeyReturn);
                    continue;
                case '\t':
                    AddVirtualKeyPress(inputs, VirtualKeyTab);
                    continue;
            }

            inputs.Add(CreateKeyboardInput(0, character, KeyEventUnicode));
            inputs.Add(CreateKeyboardInput(0, character, KeyEventUnicode | KeyEventKeyUp));
        }

        return inputs.ToArray();
    }

    private static void AddVirtualKeyPress(List<Input> inputs, ushort virtualKey)
    {
        inputs.Add(CreateKeyboardInput(virtualKey, 0, 0));
        inputs.Add(CreateKeyboardInput(virtualKey, 0, KeyEventKeyUp));
    }

    private static Input CreateKeyboardInput(ushort virtualKey, ushort scanCode, uint flags)
    {
        return new Input
        {
            Type = InputKeyboard,
            Data = new InputUnion
            {
                Keyboard = new KeyboardInput
                {
                    VirtualKey = virtualKey,
                    ScanCode = scanCode,
                    Flags = flags
                }
            }
        };
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint SendInput(uint inputCount, Input[] inputs, int inputSize);

    [DllImport("user32.dll")]
    private static extern short GetAsyncKeyState(int virtualKey);

    [StructLayout(LayoutKind.Sequential)]
    private struct Input
    {
        public uint Type;
        public InputUnion Data;
    }

    // MOUSEINPUT is the largest union member; it must be present so the
    // struct size matches what SendInput expects.
    [StructLayout(LayoutKind.Explicit)]
    private struct InputUnion
    {
        [FieldOffset(0)]
        public MouseInput Mouse;

        [FieldOffset(0)]
        public KeyboardInput Keyboard;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MouseInput
    {
        public int Dx;
        public int Dy;
        public uint MouseData;
        public uint Flags;
        public uint Time;
        public IntPtr ExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct KeyboardInput
    {
        public ushort VirtualKey;
        public ushort ScanCode;
        public uint Flags;
        public uint Time;
        public IntPtr ExtraInfo;
    }
}
