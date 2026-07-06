using FluentAssertions;
using Lighthouse.DTOs;
using Lighthouse.Services.LogStreaming;

namespace Lighthouse.Tests.Services.LogStreaming;

public class LogMergerTests
{
    private static LogEntryDto Entry(string ts, string containerId, string msg) =>
        new(ts, containerId, containerId, containerId, "stdout", msg);

    private static LogPageDto Page(bool hasMore, params LogEntryDto[] entries) =>
        new(entries.ToList(), hasMore);

    [Fact]
    public void MergeTail_InterleavesByTimestamp()
    {
        var a = Page(false,
            Entry("2026-07-04T12:00:00.000000000Z", "a", "a1"),
            Entry("2026-07-04T12:00:02.000000000Z", "a", "a2"));
        var b = Page(false,
            Entry("2026-07-04T12:00:01.000000000Z", "b", "b1"),
            Entry("2026-07-04T12:00:03.000000000Z", "b", "b2"));

        var result = LogMerger.MergeTail(new[] { a, b }, pageSize: 10);

        result.Entries.Select(e => e.Message).Should().Equal("a1", "b1", "a2", "b2");
        result.HasMore.Should().BeFalse();
    }

    [Fact]
    public void MergeTail_KeepsNewestWhenTruncated()
    {
        var a = Page(false,
            Entry("2026-07-04T12:00:00.000000000Z", "a", "old"),
            Entry("2026-07-04T12:00:02.000000000Z", "a", "mid"));
        var b = Page(false,
            Entry("2026-07-04T12:00:03.000000000Z", "b", "new"));

        var result = LogMerger.MergeTail(new[] { a, b }, pageSize: 2);

        result.Entries.Select(e => e.Message).Should().Equal("mid", "new");
        result.HasMore.Should().BeTrue(); // truncation implies more history
    }

    [Fact]
    public void MergeTail_PropagatesInputHasMore()
    {
        var a = Page(hasMore: true, Entry("2026-07-04T12:00:00.000000000Z", "a", "a1"));

        var result = LogMerger.MergeTail(new[] { a }, pageSize: 10);

        result.HasMore.Should().BeTrue();
    }

    [Fact]
    public void MergeTail_EmptyInput_ReturnsEmpty()
    {
        var result = LogMerger.MergeTail(Array.Empty<LogPageDto>(), pageSize: 10);

        result.Entries.Should().BeEmpty();
        result.HasMore.Should().BeFalse();
    }

    [Fact]
    public void MergeTail_SingleContainer_PassesThrough()
    {
        var a = Page(false,
            Entry("2026-07-04T12:00:00.000000000Z", "a", "a1"),
            Entry("2026-07-04T12:00:01.000000000Z", "a", "a2"));

        var result = LogMerger.MergeTail(new[] { a }, pageSize: 10);

        result.Entries.Select(e => e.Message).Should().Equal("a1", "a2");
    }

    [Fact]
    public void MergeTail_IdenticalTimestamps_AreStableByContainerId()
    {
        const string ts = "2026-07-04T12:00:00.000000000Z";
        var b = Page(false, Entry(ts, "b", "fromB"));
        var a = Page(false, Entry(ts, "a", "fromA"));

        var result = LogMerger.MergeTail(new[] { b, a }, pageSize: 10);

        // tie-break by container id → 'a' before 'b' regardless of input order
        result.Entries.Select(e => e.Message).Should().Equal("fromA", "fromB");
    }

    [Fact]
    public void MergeTail_ChattyContainerDoesNotStarveOthersFromHasMore()
    {
        // 'a' is chatty and its window was full (hasMore); after truncation the merged
        // page must report HasMore so the caller keeps paginating.
        var a = Page(hasMore: true,
            Entry("2026-07-04T12:00:05.000000000Z", "a", "a1"),
            Entry("2026-07-04T12:00:06.000000000Z", "a", "a2"),
            Entry("2026-07-04T12:00:07.000000000Z", "a", "a3"));
        var b = Page(false,
            Entry("2026-07-04T12:00:00.000000000Z", "b", "b_old"));

        var result = LogMerger.MergeTail(new[] { a, b }, pageSize: 2);

        result.Entries.Should().HaveCount(2);
        result.Entries.Last().Message.Should().Be("a3");
        result.HasMore.Should().BeTrue();
    }
}
