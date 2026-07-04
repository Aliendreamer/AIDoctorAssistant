using System.Threading.Channels;

namespace MedAssist.Web.Services;

/// <summary>
/// In-process FIFO queue of ingestion jobs, drained by <see cref="IngestionWorker"/>. Replaces the
/// per-request fire-and-forget <c>Task.Run</c> so long-running OCR/index work is owned by the host
/// and can be stopped cleanly on shutdown (audit P1-8). Single-reader: the worker processes jobs
/// serially, which also matches the GPU-bound, already-serialized ONNX/Marker workload.
/// </summary>
public sealed class IngestionQueue
{
    private readonly Channel<IngestionJob> _channel =
        Channel.CreateUnbounded<IngestionJob>(new UnboundedChannelOptions { SingleReader = true });

    public ValueTask EnqueueAsync(IngestionJob job, CancellationToken cancellationToken = default)
        => _channel.Writer.WriteAsync(job, cancellationToken);

    public IAsyncEnumerable<IngestionJob> DequeueAllAsync(CancellationToken cancellationToken)
        => _channel.Reader.ReadAllAsync(cancellationToken);
}
