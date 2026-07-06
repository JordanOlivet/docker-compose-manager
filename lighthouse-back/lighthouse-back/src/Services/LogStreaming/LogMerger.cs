using Lighthouse.DTOs;

namespace Lighthouse.Services.LogStreaming;

/// <summary>
/// Merges per-container history pages into one project-wide page, ordered by timestamp.
/// </summary>
public static class LogMerger
{
    /// <summary>
    /// K-way merges the given per-container pages and keeps the newest
    /// <paramref name="pageSize"/> entries, returned ascending by timestamp
    /// (tie-broken by container id for a stable order).
    /// </summary>
    /// <remarks>
    /// Gap-free by construction: each input page was fetched with a full tail window,
    /// so a container that still has older un-fetched entries necessarily filled its
    /// window — its oldest fetched entry therefore bounds the merged page's oldest,
    /// and the next <c>until</c> cursor cannot skip anything.
    /// Timestamps are compared as strings: Docker emits fixed-width RFC3339Nano in UTC,
    /// so lexical order equals chronological order.
    /// </remarks>
    public static LogPageDto MergeTail(IReadOnlyList<LogPageDto> perContainer, int pageSize)
    {
        List<LogEntryDto> all = new();
        bool anyInputHasMore = false;

        foreach (LogPageDto page in perContainer)
        {
            all.AddRange(page.Entries);
            anyInputHasMore |= page.HasMore;
        }

        all.Sort(CompareEntries);

        bool truncated = all.Count > pageSize;
        List<LogEntryDto> window = truncated
            ? all.GetRange(all.Count - pageSize, pageSize)
            : all;

        return new LogPageDto(window, HasMore: anyInputHasMore || truncated);
    }

    private static int CompareEntries(LogEntryDto a, LogEntryDto b)
    {
        int byTimestamp = string.CompareOrdinal(a.Timestamp, b.Timestamp);
        return byTimestamp != 0 ? byTimestamp : string.CompareOrdinal(a.ContainerId, b.ContainerId);
    }
}
