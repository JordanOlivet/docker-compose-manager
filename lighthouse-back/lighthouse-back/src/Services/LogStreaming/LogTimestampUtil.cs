using System.Globalization;

namespace Lighthouse.Services.LogStreaming;

/// <summary>
/// Helpers for the RFC3339Nano timestamps Docker prepends to log lines when
/// Timestamps=true, and for converting them to the unix-with-nanoseconds form the
/// Docker Engine expects for the since/until log filters.
/// </summary>
public static class LogTimestampUtil
{
    /// <summary>
    /// Splits the "2026-07-04T12:00:00.123456789Z " prefix off a log line.
    /// Returns false (empty timestamp, whole line as message) when the prefix is
    /// missing or malformed — e.g. TTY progress output using bare '\r'.
    /// </summary>
    public static bool TrySplitTimestampPrefix(string rawLine, out string timestamp, out string message)
    {
        int spaceIndex = rawLine.IndexOf(' ');
        if (spaceIndex > 0)
        {
            string candidate = rawLine[..spaceIndex];
            if (LooksLikeRfc3339(candidate))
            {
                timestamp = candidate;
                message = rawLine[(spaceIndex + 1)..];
                return true;
            }
        }

        timestamp = string.Empty;
        message = rawLine;
        return false;
    }

    /// <summary>
    /// Converts an RFC3339Nano timestamp to "unixSeconds.nanoseconds" for Docker's
    /// since/until parameters. The fractional part is carried textually because
    /// DateTimeOffset ticks (100ns) cannot represent full nanosecond precision.
    /// Throws FormatException on unparseable input.
    /// </summary>
    public static string ToUnixNano(string rfc3339Nano)
    {
        string fraction = string.Empty;
        string withoutFraction = rfc3339Nano;

        int dotIndex = rfc3339Nano.IndexOf('.');
        if (dotIndex >= 0)
        {
            int end = dotIndex + 1;
            while (end < rfc3339Nano.Length && char.IsAsciiDigit(rfc3339Nano[end]))
            {
                end++;
            }

            fraction = rfc3339Nano[(dotIndex + 1)..end];
            withoutFraction = rfc3339Nano[..dotIndex] + rfc3339Nano[end..];
        }

        DateTimeOffset parsed = DateTimeOffset.Parse(
            withoutFraction,
            CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal);

        fraction = fraction.Length > 9 ? fraction[..9] : fraction.PadRight(9, '0');
        return $"{parsed.ToUnixTimeSeconds()}.{fraction}";
    }

    private static bool LooksLikeRfc3339(string value)
    {
        return value.Length >= 20
            && value[4] == '-'
            && value[7] == '-'
            && value[10] == 'T'
            && DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.None, out _);
    }
}
