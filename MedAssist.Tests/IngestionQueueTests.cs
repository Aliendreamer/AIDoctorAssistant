using MedAssist.Web.Services;

namespace MedAssist.Tests;

// Guards P1-8: the ingestion queue must hand jobs to the worker in FIFO order.
public sealed class IngestionQueueTests
{
    private static IngestionJob Job(int id) =>
        new(IngestionJobKind.Extract, id, $"book-{id}", "T", "A", "en", "", $"/books/{id}.pdf");

    [Fact]
    public async Task Dequeue_YieldsJobsInEnqueueOrder()
    {
        var queue = new IngestionQueue();
        await queue.EnqueueAsync(Job(1));
        await queue.EnqueueAsync(Job(2));
        await queue.EnqueueAsync(Job(3));

        var seen = new List<int>();

        await foreach (var job in queue.DequeueAllAsync(CancellationToken.None))
        {
            seen.Add(job.BookId);
            if (seen.Count == 3)
            {
                break;
            }
        }

        Assert.Equal([1, 2, 3], seen);
    }
}
