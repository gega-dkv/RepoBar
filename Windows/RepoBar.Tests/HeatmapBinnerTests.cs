using RepoBar.Core.Models;
using RepoBar.Core.Support;
using Xunit;

namespace RepoBar.Tests;

public sealed class HeatmapBinnerTests
{
    [Fact]
    public void AssignsZeroIntensityToEmptyDaysAndScalesNonZeroCounts()
    {
        IReadOnlyList<HeatmapBucket> buckets = HeatmapBinner.Bucket(
            [
                new HeatmapCell(new DateOnly(2026, 5, 1), 0),
                new HeatmapCell(new DateOnly(2026, 5, 2), 1),
                new HeatmapCell(new DateOnly(2026, 5, 3), 5),
                new HeatmapCell(new DateOnly(2026, 5, 4), 10),
            ]);

        Assert.Equal([0, 1, 2, 4], buckets.Select(bucket => bucket.Intensity));
    }
}
