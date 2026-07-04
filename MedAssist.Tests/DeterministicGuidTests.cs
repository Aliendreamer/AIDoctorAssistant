using MedAssist.Shared.Models;

namespace MedAssist.Tests;

// Guards P1-6: Qdrant point ids must be derived deterministically from a chunk's identity so that
// re-indexing overwrites points instead of appending duplicates (previously ids were Guid.NewGuid()).
public sealed class DeterministicGuidTests
{
    [Fact]
    public void Create_IsStableForSameKey()
        => Assert.Equal(DeterministicGuid.Create("harrison-21:42"), DeterministicGuid.Create("harrison-21:42"));

    [Fact]
    public void Create_DiffersForDifferentKeys()
        => Assert.NotEqual(DeterministicGuid.Create("harrison-21:42"), DeterministicGuid.Create("harrison-21:43"));

    [Fact]
    public void Create_DistinguishesSummaryFromChunkAtSameIndex()
        => Assert.NotEqual(DeterministicGuid.Create("harrison-21:5"), DeterministicGuid.Create("summary:harrison-21:5"));

    [Fact]
    public void Create_IsNotEmpty()
        => Assert.NotEqual(Guid.Empty, DeterministicGuid.Create("harrison-21:0"));
}
