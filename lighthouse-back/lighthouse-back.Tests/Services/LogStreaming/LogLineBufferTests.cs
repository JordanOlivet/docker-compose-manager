using System.Text;
using Docker.DotNet;
using FluentAssertions;
using Lighthouse.Services.LogStreaming;

namespace Lighthouse.Tests.Services.LogStreaming;

public class LogLineBufferTests
{
    private static ReadOnlySpan<byte> Utf8(string s) => Encoding.UTF8.GetBytes(s);

    [Fact]
    public void SingleCompleteLine_IsYielded()
    {
        var buffer = new LogLineBuffer();
        buffer.Feed(MultiplexedStream.TargetStream.StandardOut, Utf8("hello\n"));

        var lines = buffer.DrainCompletedLines().ToList();

        lines.Should().ContainSingle();
        lines[0].Line.Should().Be("hello");
        lines[0].Stream.Should().Be(LogLineBuffer.StdoutStream);
    }

    [Fact]
    public void LineSplitAcrossTwoFeeds_IsReassembled()
    {
        var buffer = new LogLineBuffer();
        buffer.Feed(MultiplexedStream.TargetStream.StandardOut, Utf8("hel"));
        buffer.DrainCompletedLines().Should().BeEmpty();

        buffer.Feed(MultiplexedStream.TargetStream.StandardOut, Utf8("lo\n"));
        var lines = buffer.DrainCompletedLines().ToList();

        lines.Should().ContainSingle();
        lines[0].Line.Should().Be("hello");
    }

    [Fact]
    public void MultiByteCharSplitAcrossReads_DecodesCorrectly()
    {
        // 'é' (U+00E9) is 0xC3 0xA9 in UTF-8; split it across two feeds.
        byte[] full = Encoding.UTF8.GetBytes("café\n");
        int splitAt = Array.IndexOf(full, (byte)0xC3) + 1; // between the two bytes of 'é'

        var buffer = new LogLineBuffer();
        buffer.Feed(MultiplexedStream.TargetStream.StandardOut, full.AsSpan(0, splitAt));
        buffer.Feed(MultiplexedStream.TargetStream.StandardOut, full.AsSpan(splitAt));

        var lines = buffer.DrainCompletedLines().ToList();
        lines.Should().ContainSingle();
        lines[0].Line.Should().Be("café");
    }

    [Fact]
    public void MultipleLinesInOneChunk_AreAllYielded()
    {
        var buffer = new LogLineBuffer();
        buffer.Feed(MultiplexedStream.TargetStream.StandardOut, Utf8("a\nb\nc\n"));

        var lines = buffer.DrainCompletedLines().Select(l => l.Line).ToList();

        lines.Should().Equal("a", "b", "c");
    }

    [Fact]
    public void TrailingCarriageReturn_IsStripped()
    {
        var buffer = new LogLineBuffer();
        buffer.Feed(MultiplexedStream.TargetStream.StandardOut, Utf8("windows\r\n"));

        var lines = buffer.DrainCompletedLines().ToList();
        lines[0].Line.Should().Be("windows");
    }

    [Fact]
    public void StdoutAndStderr_AreTaggedIndependently()
    {
        var buffer = new LogLineBuffer();
        buffer.Feed(MultiplexedStream.TargetStream.StandardOut, Utf8("out\n"));
        buffer.Feed(MultiplexedStream.TargetStream.StandardError, Utf8("err\n"));

        var lines = buffer.DrainCompletedLines().ToList();

        lines.Should().HaveCount(2);
        lines.Should().Contain(l => l.Line == "out" && l.Stream == LogLineBuffer.StdoutStream);
        lines.Should().Contain(l => l.Line == "err" && l.Stream == LogLineBuffer.StderrStream);
    }

    [Fact]
    public void InterleavedStdoutStderr_KeepSeparateAccumulators()
    {
        var buffer = new LogLineBuffer();
        buffer.Feed(MultiplexedStream.TargetStream.StandardOut, Utf8("ou"));
        buffer.Feed(MultiplexedStream.TargetStream.StandardError, Utf8("er"));
        buffer.Feed(MultiplexedStream.TargetStream.StandardOut, Utf8("t\n"));
        buffer.Feed(MultiplexedStream.TargetStream.StandardError, Utf8("r\n"));

        var lines = buffer.DrainCompletedLines().Select(l => l.Line).ToList();
        lines.Should().Contain("out");
        lines.Should().Contain("err");
    }

    [Fact]
    public void Flush_EmitsTrailingUnterminatedLine()
    {
        var buffer = new LogLineBuffer();
        buffer.Feed(MultiplexedStream.TargetStream.StandardOut, Utf8("no newline"));
        buffer.DrainCompletedLines().Should().BeEmpty();

        buffer.Flush();
        var lines = buffer.DrainCompletedLines().ToList();

        lines.Should().ContainSingle();
        lines[0].Line.Should().Be("no newline");
    }

    [Fact]
    public void Flush_WithNoPendingBytes_YieldsNothing()
    {
        var buffer = new LogLineBuffer();
        buffer.Feed(MultiplexedStream.TargetStream.StandardOut, Utf8("done\n"));
        buffer.DrainCompletedLines().ToList();

        buffer.Flush();
        buffer.DrainCompletedLines().Should().BeEmpty();
    }
}
