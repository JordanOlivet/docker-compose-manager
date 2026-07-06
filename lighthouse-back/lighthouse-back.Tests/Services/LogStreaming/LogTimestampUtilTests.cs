using FluentAssertions;
using Lighthouse.Services.LogStreaming;

namespace Lighthouse.Tests.Services.LogStreaming;

public class LogTimestampUtilTests
{
    [Fact]
    public void TrySplitTimestampPrefix_WithNanoFraction_SplitsCorrectly()
    {
        bool ok = LogTimestampUtil.TrySplitTimestampPrefix(
            "2026-07-04T12:00:00.123456789Z hello world", out string ts, out string msg);

        ok.Should().BeTrue();
        ts.Should().Be("2026-07-04T12:00:00.123456789Z");
        msg.Should().Be("hello world");
    }

    [Fact]
    public void TrySplitTimestampPrefix_WithoutFraction_SplitsCorrectly()
    {
        bool ok = LogTimestampUtil.TrySplitTimestampPrefix(
            "2026-07-04T12:00:00Z message", out string ts, out string msg);

        ok.Should().BeTrue();
        ts.Should().Be("2026-07-04T12:00:00Z");
        msg.Should().Be("message");
    }

    [Fact]
    public void TrySplitTimestampPrefix_NoPrefix_ReturnsFalseAndWholeLine()
    {
        bool ok = LogTimestampUtil.TrySplitTimestampPrefix(
            "just a plain line", out string ts, out string msg);

        ok.Should().BeFalse();
        ts.Should().BeEmpty();
        msg.Should().Be("just a plain line");
    }

    [Fact]
    public void TrySplitTimestampPrefix_EmptyLine_ReturnsFalse()
    {
        bool ok = LogTimestampUtil.TrySplitTimestampPrefix("", out string ts, out string msg);

        ok.Should().BeFalse();
        ts.Should().BeEmpty();
        msg.Should().BeEmpty();
    }

    [Fact]
    public void ToUnixNano_PreservesFullNanosecondPrecision()
    {
        // 2026-07-04T12:00:00Z == 1783166400 unix seconds
        string result = LogTimestampUtil.ToUnixNano("2026-07-04T12:00:00.123456789Z");

        result.Should().Be("1783166400.123456789");
    }

    [Fact]
    public void ToUnixNano_NoFraction_PadsToNineZeros()
    {
        string result = LogTimestampUtil.ToUnixNano("2026-07-04T12:00:00Z");

        result.Should().Be("1783166400.000000000");
    }

    [Fact]
    public void ToUnixNano_ShortFraction_RightPads()
    {
        string result = LogTimestampUtil.ToUnixNano("2026-07-04T12:00:00.5Z");

        result.Should().Be("1783166400.500000000");
    }

    [Fact]
    public void ToUnixNano_Malformed_Throws()
    {
        Action act = () => LogTimestampUtil.ToUnixNano("not-a-timestamp");

        act.Should().Throw<FormatException>();
    }
}
