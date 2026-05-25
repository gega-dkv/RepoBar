using RepoBar.Core.Models;

namespace RepoBar.Core.Support;

public static class HeatmapBinner
{
    public static IReadOnlyList<HeatmapBucket> Bucket(IEnumerable<HeatmapCell> cells)
    {
        HeatmapCell[] materialized = cells.ToArray();
        int max = materialized.Length == 0 ? 0 : materialized.Max(cell => cell.Count);

        return materialized
            .Select(cell => new HeatmapBucket(cell.Date, cell.Count, Intensity(cell.Count, max)))
            .ToArray();
    }

    public static int Intensity(int count, int maxCount)
    {
        if (count <= 0 || maxCount <= 0)
        {
            return 0;
        }

        double ratio = (double)count / maxCount;
        return ratio switch
        {
            <= 0.25 => 1,
            <= 0.5 => 2,
            <= 0.75 => 3,
            _ => 4,
        };
    }
}

public sealed record HeatmapBucket(DateOnly Date, int Count, int Intensity);
