using System.Threading;
using System.Windows.Forms;
using Stt.Core.Abstractions;
using Stt.Core.Diagnostics;

namespace Stt.App.Services;

public sealed class WindowsPasteShortcutService : IPasteShortcutService
{
    private static readonly TimeSpan SendTimeout = TimeSpan.FromSeconds(2);

    public bool TrySendPasteShortcut()
    {
        Exception? failure = null;
        var completed = false;

        var thread = new Thread(() =>
        {
            try
            {
                SendKeys.SendWait("^v");
                completed = true;
            }
            catch (Exception ex)
            {
                failure = ex;
            }
        });

        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();

        if (!thread.Join(SendTimeout))
        {
            WhisperTrace.Log("PasteShortcut", "Ctrl+V timed out before completion.");
            return false;
        }

        if (failure is not null)
        {
            WhisperTrace.Log("PasteShortcut", $"Ctrl+V failed. {failure.Message}");
            return false;
        }

        WhisperTrace.Log("PasteShortcut", "Sent Ctrl+V to the current focus.");
        return completed;
    }
}
