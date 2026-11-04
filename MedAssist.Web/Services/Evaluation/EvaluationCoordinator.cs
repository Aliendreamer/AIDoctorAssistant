using System.Threading.Channels;

namespace MedAssist.Web.Services.Evaluation;

/// <summary>
/// Admits one evaluation run at a time and hands it to <see cref="EvaluationWorker"/>. A second run
/// would compete for the same GPU and spoil both runs' latency numbers.
/// </summary>
public sealed class EvaluationCoordinator
{
    private readonly Channel<Guid> _channel =
        Channel.CreateUnbounded<Guid>(new UnboundedChannelOptions { SingleReader = true });

    private readonly Lock _gate = new();
    private Guid? _activeRunId;
    private CancellationTokenSource? _cancellation;

    public Guid? ActiveRunId
    {
        get
        {
            lock (_gate)
            {
                return _activeRunId;
            }
        }
    }

    /// <summary>Claims the single run slot; false when a run already holds it.</summary>
    public bool TryReserve(Guid runId)
    {
        lock (_gate)
        {
            if (_activeRunId is not null)
            {
                return false;
            }

            _activeRunId = runId;
            _cancellation = new CancellationTokenSource();
            return true;
        }
    }

    public ValueTask EnqueueAsync(Guid runId, CancellationToken cancellationToken = default) =>
        _channel.Writer.WriteAsync(runId, cancellationToken);

    public IAsyncEnumerable<Guid> DequeueAllAsync(CancellationToken cancellationToken) =>
        _channel.Reader.ReadAllAsync(cancellationToken);

    /// <summary>The active run's cancellation token.</summary>
    public CancellationToken TokenFor(Guid runId)
    {
        lock (_gate)
        {
            return _activeRunId == runId && _cancellation is not null ? _cancellation.Token : new CancellationToken(canceled: true);
        }
    }

    public bool Cancel(Guid runId)
    {
        lock (_gate)
        {
            if (_activeRunId != runId || _cancellation is null)
            {
                return false;
            }

            _cancellation.Cancel();
            return true;
        }
    }

    /// <summary>Frees the run slot.</summary>
    public void Release(Guid runId)
    {
        lock (_gate)
        {
            if (_activeRunId != runId)
            {
                return;
            }

            _cancellation?.Dispose();
            _cancellation = null;
            _activeRunId = null;
        }
    }
}
