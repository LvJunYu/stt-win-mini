using System.IO;
using System.Media;
using System.Reflection;
using Stt.Core.Diagnostics;

namespace Stt.App.Services;

public static class RecordingReadySoundCuePlayer
{
    private const string CueResourceName = "Stt.App.Assets.recording-ready.wav";
    private static readonly Lazy<CachedCue?> CachedBundledCue = new(CreateBundledCue);

    public static void Play()
    {
        try
        {
            if (TryPlayBundledCue())
            {
                return;
            }

            SystemSounds.Asterisk.Play();
        }
        catch (Exception ex)
        {
            WhisperTrace.Log("RecordingReadyCue", $"Failed to play recording-ready sound cue. {ex.Message}");
        }
    }

    private static bool TryPlayBundledCue()
    {
        var cue = CachedBundledCue.Value;
        if (cue is null)
        {
            return false;
        }

        cue.Player.Play();
        return true;
    }

    private static CachedCue? CreateBundledCue()
    {
        var assembly = Assembly.GetExecutingAssembly();
        using var resourceStream = assembly.GetManifestResourceStream(CueResourceName);
        if (resourceStream is null)
        {
            WhisperTrace.Log("RecordingReadyCue", $"Bundled sound resource was not found: {CueResourceName}");
            return null;
        }

        var buffer = new MemoryStream();
        resourceStream.CopyTo(buffer);
        buffer.Position = 0;

        var player = new SoundPlayer(buffer);
        player.Load();
        return new CachedCue(buffer, player);
    }

    private sealed class CachedCue
    {
        public CachedCue(MemoryStream stream, SoundPlayer player)
        {
            Stream = stream;
            Player = player;
        }

        public MemoryStream Stream { get; }
        public SoundPlayer Player { get; }
    }
}
