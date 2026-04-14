using Stt.Core.Abstractions;
using Stt.Core.Diagnostics;
using Stt.Core.Models;

namespace Stt.Infrastructure.Workflows;

public sealed class ProviderSelectableRecordingWorkflow : IRecordingWorkflow, IRecordingWorkflowModeProvider, IRecordingWorkflowStartupNotifier, IRecordingWorkflowDeferredStop
{
    private readonly IRecordingWorkflow _primaryWorkflow;
    private readonly Func<bool> _usePrimaryWorkflowAccessor;
    private readonly IRecordingWorkflow _secondaryWorkflow;
    private readonly string _primaryLabel;
    private readonly string _secondaryLabel;
    private readonly object _syncRoot = new();
    private IRecordingWorkflow? _activeWorkflow;

    public ProviderSelectableRecordingWorkflow(
        Func<bool> usePrimaryWorkflowAccessor,
        IRecordingWorkflow primaryWorkflow,
        IRecordingWorkflow secondaryWorkflow,
        string primaryLabel,
        string secondaryLabel)
    {
        _usePrimaryWorkflowAccessor = usePrimaryWorkflowAccessor;
        _primaryWorkflow = primaryWorkflow;
        _secondaryWorkflow = secondaryWorkflow;
        _primaryLabel = primaryLabel;
        _secondaryLabel = secondaryLabel;

        _primaryWorkflow.TranscriptUpdated += OnTranscriptUpdated;
        _secondaryWorkflow.TranscriptUpdated += OnTranscriptUpdated;

        if (_primaryWorkflow is IRecordingWorkflowStartupNotifier primaryStartupNotifier)
        {
            primaryStartupNotifier.RecordingStarted += OnRecordingStarted;
        }

        if (_secondaryWorkflow is IRecordingWorkflowStartupNotifier secondaryStartupNotifier)
        {
            secondaryStartupNotifier.RecordingStarted += OnRecordingStarted;
        }
    }

    public event EventHandler<TranscriptUpdatedEventArgs>? TranscriptUpdated;
    public event EventHandler? RecordingStarted;

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var usePrimaryWorkflow = _usePrimaryWorkflowAccessor();
        var selectedWorkflow = usePrimaryWorkflow
            ? _primaryWorkflow
            : _secondaryWorkflow;

        WhisperTrace.Log(
            "ProviderWorkflow",
            $"Selected {GetSelectedLabel(usePrimaryWorkflow)} workflow for this session.");

        lock (_syncRoot)
        {
            _activeWorkflow = selectedWorkflow;
        }

        try
        {
            await selectedWorkflow.StartAsync(cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            lock (_syncRoot)
            {
                _activeWorkflow = null;
            }

            throw;
        }
    }

    public async Task<TranscriptResult> StopAndTranscribeAsync(CancellationToken cancellationToken)
    {
        IRecordingWorkflow activeWorkflow;

        lock (_syncRoot)
        {
            activeWorkflow = _activeWorkflow
                ?? throw new InvalidOperationException("No active recording session was found.");
        }

        try
        {
            return await activeWorkflow.StopAndTranscribeAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            lock (_syncRoot)
            {
                _activeWorkflow = null;
            }
        }
    }

    public async Task<PendingTranscription> StopForDeferredTranscriptionAsync(CancellationToken cancellationToken)
    {
        IRecordingWorkflow activeWorkflow;

        lock (_syncRoot)
        {
            activeWorkflow = _activeWorkflow
                ?? throw new InvalidOperationException("No active recording session was found.");
        }

        if (activeWorkflow is not IRecordingWorkflowDeferredStop deferredStop)
        {
            throw new InvalidOperationException("Deferred stop is unavailable for the current recording workflow.");
        }

        try
        {
            return await deferredStop.StopForDeferredTranscriptionAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            lock (_syncRoot)
            {
                _activeWorkflow = null;
            }
        }
    }

    public RecordingWorkflowMode GetCurrentMode()
    {
        lock (_syncRoot)
        {
            return _activeWorkflow is IRecordingWorkflowModeProvider provider
                ? provider.GetCurrentMode()
                : RecordingWorkflowMode.Unknown;
        }
    }

    private void OnTranscriptUpdated(object? sender, TranscriptUpdatedEventArgs e)
    {
        lock (_syncRoot)
        {
            if (!ReferenceEquals(sender, _activeWorkflow))
            {
                return;
            }
        }

        TranscriptUpdated?.Invoke(this, e);
    }

    private void OnRecordingStarted(object? sender, EventArgs e)
    {
        lock (_syncRoot)
        {
            if (!ReferenceEquals(sender, _activeWorkflow))
            {
                return;
            }
        }

        RecordingStarted?.Invoke(this, EventArgs.Empty);
    }

    private string GetSelectedLabel(bool usePrimaryWorkflow)
    {
        return usePrimaryWorkflow
            ? _primaryLabel
            : _secondaryLabel;
    }
}
