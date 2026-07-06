using System.Runtime.InteropServices;
using System.Text;
using Docker.DotNet;

namespace Lighthouse.Services.LogStreaming;

/// <summary>
/// Accumulates raw bytes from a Docker log stream, per output target, and yields only
/// completed lines decoded as UTF-8. Decoding happens after line assembly, so a read
/// boundary falling inside a line — or inside a multi-byte character — never produces
/// partial or corrupted output.
/// </summary>
public class LogLineBuffer
{
    public const string StdoutStream = "stdout";
    public const string StderrStream = "stderr";

    private readonly List<byte> _stdout = new();
    private readonly List<byte> _stderr = new();
    private readonly Queue<(string Line, string Stream)> _completed = new();

    /// <summary>
    /// Feeds a chunk of raw bytes read from the stream for the given target.
    /// TTY streams have a single output; feed them as StandardOut.
    /// </summary>
    public void Feed(MultiplexedStream.TargetStream target, ReadOnlySpan<byte> data)
    {
        bool isStderr = target == MultiplexedStream.TargetStream.StandardError;
        List<byte> accumulator = isStderr ? _stderr : _stdout;
        string streamName = isStderr ? StderrStream : StdoutStream;

        int start = 0;
        for (int i = 0; i < data.Length; i++)
        {
            if (data[i] != (byte)'\n')
            {
                continue;
            }

            AppendRange(accumulator, data[start..i]);
            _completed.Enqueue((DecodeAndClear(accumulator), streamName));
            start = i + 1;
        }

        AppendRange(accumulator, data[start..]);
    }

    /// <summary>
    /// Returns all lines completed since the last drain, in arrival order.
    /// </summary>
    public IEnumerable<(string Line, string Stream)> DrainCompletedLines()
    {
        while (_completed.Count > 0)
        {
            yield return _completed.Dequeue();
        }
    }

    /// <summary>
    /// Emits any trailing unterminated line. Call once at end of stream.
    /// </summary>
    public void Flush()
    {
        if (_stdout.Count > 0)
        {
            _completed.Enqueue((DecodeAndClear(_stdout), StdoutStream));
        }
        if (_stderr.Count > 0)
        {
            _completed.Enqueue((DecodeAndClear(_stderr), StderrStream));
        }
    }

    private static void AppendRange(List<byte> accumulator, ReadOnlySpan<byte> data)
    {
        if (!data.IsEmpty)
        {
            accumulator.AddRange(data);
        }
    }

    private static string DecodeAndClear(List<byte> accumulator)
    {
        ReadOnlySpan<byte> bytes = CollectionsMarshal.AsSpan(accumulator);
        if (bytes.Length > 0 && bytes[^1] == (byte)'\r')
        {
            bytes = bytes[..^1];
        }

        string line = Encoding.UTF8.GetString(bytes);
        accumulator.Clear();
        return line;
    }
}
