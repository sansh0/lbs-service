using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Order;

namespace LbsParser;

[MemoryDiagnoser]
[Orderer(SummaryOrderPolicy.FastestToSlowest)]
public class Parsers
{
    [Benchmark]
    public async Task RegexParser()
    {
        var regex = new Regex(@"^GSM,
                              (?'mcc'\d+),
                              (?'mnc'\d+),
                              (?'lac'\d+),
                              (?'cid'\d+),
                              (-?\d+),
                              (?'lng'-?\d+\.\d+),
                              (?'lat'-?\d+\.\d+)
                              (,\d+){6}$",
            RegexOptions.Compiled |
            RegexOptions.CultureInvariant |
            RegexOptions.IgnorePatternWhitespace |
            RegexOptions.Multiline);

        using var reader = File.OpenText("257.csv");
        await using var writer = File.CreateText("257-1.csv");
        while (await reader.ReadLineAsync() is { } line)
        {
            var match = regex.Match(line);
            if (match.Success)
            {
                var mcc = match.Groups["mcc"].Value;
                var mnc = match.Groups["mnc"].Value;
                var lac = match.Groups["lac"].Value;
                var cid = match.Groups["cid"].Value;
                var lat = match.Groups["lat"].Value;
                var lng = match.Groups["lng"].Value;

                await writer.WriteAsync($"{mcc},{mnc},{lac},{cid},{lng},{lat}\n");
            }
        }
    }

    [Benchmark]
    public async Task ValueParser()
    {
        Thread.CurrentThread.CurrentCulture = CultureInfo.InvariantCulture;
        using var reader = File.OpenText("257.csv");
        await using var writer = File.CreateText("257-2.csv");
        var sb = new StringBuilder(64);
        while (await reader.ReadLineAsync() is { } line)
        {
            sb.Clear();

            int pos = 0;
            if (TryReadType(ref pos) &&
                TryReadInt32(ref pos) &&
                TryReadInt32(ref pos) &&
                TryReadInt32(ref pos) &&
                TryReadInt32(ref pos) &&
                TrySkip(ref pos) &&
                TryReadDecimal(ref pos) &&
                TryReadDecimal(ref pos))
            {
                await writer.WriteAsync(sb.Append('\n'));
            }

            bool TryReadType(ref int from)
            {
                Debug.Assert(from == 0);
                if (line.Length < 4 || line[0] != 'G' || line[1] != 'S' || line[2] != 'M' || line[3] != ',')
                    return false;
                from = 3;
                return true;
            }

            bool TryReadInt32(ref int from)
            {
                Debug.Assert(line[from] == ',');
                int till = line.IndexOf(',', ++from);
                if (till == -1 || !int.TryParse(line.AsSpan()[from..till], out var value))
                    return false;

                if (sb.Length > 0)
                    sb.Append(',');
                sb.Append(value);

                from = till;
                return true;
            }

            bool TrySkip(ref int from)
            {
                Debug.Assert(line[from] == ',');
                int till = line.IndexOf(',', ++from);
                if (till == -1)
                    return false;
                from = till;
                return true;
            }

            bool TryReadDecimal(ref int from)
            {
                Debug.Assert(line[from] == ',');
                int till = line.IndexOf(',', ++from);
                if (till == -1 || !decimal.TryParse(line.AsSpan()[from..till], out var value))
                    return false;

                if (sb.Length > 0)
                    sb.Append(',');
                sb.Append(value);

                from = till;
                return true;
            }
        }
    }

    [Benchmark]
    public async Task LineParser()
    {
        using var reader = File.OpenText("257.csv");
        await using var writer = File.CreateText("257-3.csv");
        var sb = new StringBuilder(64);
        while (await reader.ReadLineAsync() is { } line)
        {
            sb.Clear();

            int pos = 0;
            if (TryReadType(ref pos) &&
                TryReadInt32(ref pos) &&
                TryReadInt32(ref pos) &&
                TryReadInt32(ref pos) &&
                TryReadInt32(ref pos) &&
                TrySkip(ref pos) &&
                TryReadDecimal(ref pos) &&
                TryReadDecimal(ref pos))
            {
                await writer.WriteAsync(sb.Append('\n'));
            }

            bool TryReadType(ref int from)
            {
                Debug.Assert(from == 0);
                if (line.Length < 4 || line[0] != 'G' || line[1] != 'S' || line[2] != 'M' || line[3] != ',')
                    return false;
                from = 3;
                return true;
            }

            bool TryReadInt32(ref int from)
            {
                Debug.Assert(line[from] == ',');
                if (sb.Length > 0)
                    sb.Append(',');

                while (++from < line.Length)
                {
                    var c = line[from];
                    if (c == ',')
                        return true;

                    if (!char.IsDigit(c))
                        return false;

                    sb.Append(c);
                }
                return true;
            }

            bool TrySkip(ref int from)
            {
                Debug.Assert(line[from] == ',');
                int till = line.IndexOf(',', ++from);
                if (till == -1)
                    return false;
                from = till;
                return true;
            }

            bool TryReadDecimal(ref int from)
            {
                Debug.Assert(line[from] == ',');
                if (sb.Length > 0)
                    sb.Append(',');

                while (++from < line.Length)
                {
                    var c = line[from];
                    if (c == ',')
                        return true;

                    if (!char.IsDigit(c) && c != '.')
                        return false;

                    sb.Append(c);
                }
                return true;
            }
        }
    }

    [Benchmark]
    public async Task CharParser()
    {
        using var reader = File.OpenText("257.csv");
        await using var writer = File.CreateText("257-4.csv");
        var sb = new StringBuilder(64);
        while (!reader.EndOfStream)
        {
            sb.Clear();

            if (TryReadType() &&
                TryReadInt32() &&
                TryReadInt32() &&
                TryReadInt32() &&
                TryReadInt32() &&
                TrySkip() &&
                TryReadDecimal() &&
                TryReadDecimal())
            {
                await writer.WriteAsync(sb.Append('\n'));
            }

            SkipLine();

            bool TryReadType() => reader.Read() == 'G' && reader.Read() == 'S' && reader.Read() == 'M' && reader.Read() == ',';

            bool TryReadInt32()
            {
                if (sb.Length > 0)
                    sb.Append(',');

                while (true)
                {
                    var c = (char)reader.Read();
                    if (c == ',')
                        return true;

                    if (!char.IsDigit(c))
                        return false;

                    sb.Append(c);
                }
            }

            bool TrySkip()
            {
                while (true)
                {
                    if (reader.Read() == ',')
                        return true;
                }
            }

            bool TryReadDecimal()
            {
                if (sb.Length > 0)
                    sb.Append(',');

                while (true)
                {
                    var c = (char)reader.Read();
                    if (c == ',')
                        return true;

                    if (!char.IsDigit(c) && c != '.')
                        return false;

                    sb.Append(c);
                }
            }

            void SkipLine()
            {
                while (!reader.EndOfStream)
                {
                    if (reader.Read() == '\n')
                        return;
                }
            }
        }
    }
}
