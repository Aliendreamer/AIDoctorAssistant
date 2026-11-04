using MedAssist.Data;
using MedAssist.Data.Entities;
using MedAssist.Data.Repositories;
using MedAssist.Shared.Models.Evaluation;

namespace MedAssist.Tests;

public sealed class EvaluationRepositoryTests : IDisposable
{
    private readonly EvalTestDb _testDb = new();
    private readonly MedAssistDbContext _db;
    private readonly EvaluationRepository _sut;

    public EvaluationRepositoryTests()
    {
        _db = _testDb.CreateContext();
        _sut = new EvaluationRepository(_db);
    }

    public void Dispose()
    {
        _db.Dispose();
        _testDb.Dispose();
    }

    private async Task<Guid> AddRunAsync(EvalRunStatus status = EvalRunStatus.Completed, int minutesAgo = 0)
    {
        var id = Guid.NewGuid();
        await _sut.AddRunAsync(new EvaluationRunEntity
        {
            Id = id,
            Status = status,
            CreatedAt = DateTimeOffset.UtcNow.AddMinutes(-minutesAgo),
            Snapshot = "{}",
        });
        return id;
    }

    [Fact]
    public async Task ListRuns_NewestFirstAndLimited()
    {
        var old = await AddRunAsync(minutesAgo: 30);
        var newest = await AddRunAsync(minutesAgo: 1);
        await AddRunAsync(minutesAgo: 10);

        var runs = await _sut.ListRunsAsync(2);

        Assert.Equal(2, runs.Count);
        Assert.Equal(newest, runs[0].Id);
        Assert.DoesNotContain(runs, r => r.Id == old);
    }

    [Fact]
    public async Task SaveResult_InsertsThenReplaces()
    {
        var run = await AddRunAsync();

        await _sut.SaveResultAsync(run, "G1", 0, "answered");
        await _sut.SaveResultAsync(run, "G1", 0, "judged");

        var rows = await _sut.GetResultsAsync(run);
        Assert.Equal("judged", Assert.Single(rows).Details);
    }

    [Fact]
    public async Task GetResults_FollowsTheGoldenOrder()
    {
        var run = await AddRunAsync();
        await _sut.SaveResultAsync(run, "G2", 1, "b");
        await _sut.SaveResultAsync(run, "G1", 0, "a");

        var rows = await _sut.GetResultsAsync(run);

        Assert.Equal(["G1", "G2"], rows.Select(r => r.CaseId));
    }

    [Fact]
    public async Task UpdateProgress_And_Finish_ArePersisted()
    {
        var run = await AddRunAsync(EvalRunStatus.Queued);

        await _sut.UpdateProgressAsync(run, EvalRunStatus.Running, "Answering", 3, 40);
        var running = await _sut.GetRunAsync(run);
        await _sut.FinishAsync(run, EvalRunStatus.Completed, "{\"Cases\":1}", null);
        var finished = await _sut.GetRunAsync(run);

        Assert.Equal(EvalRunStatus.Running, running!.Status);
        Assert.Equal(3, running.Done);
        Assert.Equal(EvalRunStatus.Completed, finished!.Status);
        Assert.Equal(string.Empty, finished.Phase);
        Assert.NotNull(finished.Summary);
        Assert.NotNull(finished.CompletedAt);
    }

    [Fact]
    public async Task SetBaseline_KeepsExactlyOne()
    {
        var first = await AddRunAsync();
        var second = await AddRunAsync();

        await _sut.SetBaselineAsync(first, true);
        await _sut.SetBaselineAsync(second, true);

        Assert.False((await _sut.GetRunAsync(first))!.IsBaseline);
        Assert.True((await _sut.GetRunAsync(second))!.IsBaseline);

        await _sut.SetBaselineAsync(second, false);

        Assert.False((await _sut.GetRunAsync(second))!.IsBaseline);
    }

    [Fact]
    public async Task DeleteRun_RemovesItsResults()
    {
        var run = await AddRunAsync();
        await _sut.SaveResultAsync(run, "G1", 0, "a");

        await _sut.DeleteRunAsync(run);

        Assert.Null(await _sut.GetRunAsync(run));
        Assert.Empty(await _sut.GetResultsAsync(run));
    }

    [Fact]
    public async Task FailInterrupted_TouchesOnlyActiveRuns()
    {
        var running = await AddRunAsync(EvalRunStatus.Running);
        var queued = await AddRunAsync(EvalRunStatus.Queued);
        var done = await AddRunAsync(EvalRunStatus.Completed);

        var count = await _sut.FailInterruptedAsync("restart");

        Assert.Equal(2, count);
        Assert.Equal(EvalRunStatus.Failed, (await _sut.GetRunAsync(running))!.Status);
        Assert.Equal("restart", (await _sut.GetRunAsync(queued))!.Error);
        Assert.Equal(EvalRunStatus.Completed, (await _sut.GetRunAsync(done))!.Status);
    }

    [Fact]
    public async Task ClearByUserPrefix_LeavesRealUsersAlone()
    {
        var chat = new ChatHistoryRepository(_db);
        await chat.AddMessagesAsync(
        [
            new() { UserId = "eval:run:G1", QueryType = "disease", Role = "user", Content = "q", CreatedAt = DateTimeOffset.UtcNow },
            new() { UserId = "dr.ivanova", QueryType = "disease", Role = "user", Content = "q", CreatedAt = DateTimeOffset.UtcNow },
        ]);

        await chat.ClearByUserPrefixAsync("eval:run:");

        Assert.Empty(await chat.GetRecentAsync("eval:run:G1", "disease"));
        Assert.Single(await chat.GetRecentAsync("dr.ivanova", "disease"));
    }
}
