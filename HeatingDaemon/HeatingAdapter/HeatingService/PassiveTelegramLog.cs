using System.Globalization;
using System.Text;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace HeatingDaemon;

/// <summary>
/// Collects long-term statistics of passive Elster telegrams and persists them
/// in a human readable plain text file. The class is thread-safe.
/// </summary>
public class PassiveTelegramLog
{
    private const string HeaderKey = "[key] ";
    private const string HeaderVal = "[val] ";
    private const string LastValueMarker = " last_value=";
    private const string ValueMarker = " value=";

    private readonly ILogger<PassiveTelegramLog> _logger;
    private readonly PassiveTelegramLogConfig _config;
    private readonly object _lock = new();
    private readonly object _flushLock = new();
    private readonly Dictionary<TelegramKey, TelegramStat> _stats = new();
    private int _flushRequested;

    public PassiveTelegramLog(IOptions<PassiveTelegramLogConfig> config,
                              ILogger<PassiveTelegramLog> logger)
    {
        _logger = logger;
        _config = config.Value;
        if (_config.Enabled)
        {
            Load();
        }
    }

    public bool Enabled => _config.Enabled;

    public bool FlushRequested => Volatile.Read(ref _flushRequested) != 0;

    public void RequestFlush() => Interlocked.Exchange(ref _flushRequested, 1);

    public void ClearFlushRequest() => Interlocked.Exchange(ref _flushRequested, 0);

    /// <summary>
    /// Adds a received passive telegram to the statistics.
    /// </summary>
    public void Observe(ElsterCANFrame frame)
    {
        if (!_config.Enabled) return;
        if (!frame.IsValidTelegram) return;
        if (frame.TelegramType == ElsterTelegramType.Read) return;

        string hex;
        string interpreted;
        if (frame.Value != null)
        {
            hex = frame.Value.ToHexString();
            interpreted = Sanitize(frame.Value.ToString());
        }
        else if (_config.IncludeUnknownIndices && frame.RawValue is ushort rawValue)
        {
            hex = rawValue.ToString("X4");
            interpreted = "unknown";
        }
        else
        {
            return;
        }

        DateTime now = DateTime.UtcNow;
        TelegramKey key = new(frame.SenderCanId, frame.ReceiverCanId, frame.ElsterIndex, frame.TelegramType);

        lock (_lock)
        {
            if (!_stats.TryGetValue(key, out TelegramStat? stat))
            {
                stat = new TelegramStat
                {
                    SenderCanId = frame.SenderCanId,
                    ReceiverCanId = frame.ReceiverCanId,
                    ElsterIndex = frame.ElsterIndex,
                    Name = frame.ElsterName,
                    TelegramType = frame.TelegramType,
                    FirstSeenUtc = now
                };
                _stats.Add(key, stat);
            }

            stat.Count++;
            stat.LastSeenUtc = now;
            stat.LastValueHex = hex;
            stat.LastValue = interpreted;

            if (!stat.Values.TryGetValue(hex, out ValueStat? value))
            {
                value = new ValueStat
                {
                    Hex = hex,
                    Value = interpreted,
                    FirstSeenUtc = now
                };
                stat.Values.Add(hex, value);
            }

            value.Count++;
            value.Value = interpreted;
            value.LastSeenUtc = now;
        }
    }

    /// <summary>
    /// Writes the current statistics to the configured plain text file.
    /// </summary>
    public void Flush()
    {
        if (!_config.Enabled) return;

        lock (_flushLock)
        {
            string content;
            int keyCount;
            lock (_lock)
            {
                content = BuildReport();
                keyCount = _stats.Count;
            }

            try
            {
                string path = Path.GetFullPath(_config.OutputFile);
                string? directory = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                string tempPath = path + ".tmp";
                File.WriteAllText(tempPath, content, Encoding.UTF8);
                File.Move(tempPath, path, true);
                _logger.LogInformation("Passive telegram statistics ({count} telegram types) written to {path}", keyCount, path);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Could not write passive telegram statistics to {path}", _config.OutputFile);
            }
        }
    }

    private string BuildReport()
    {
        StringBuilder sb = new();
        sb.AppendLine("# PassiveTelegramStats v1");
        sb.AppendLine($"# updated: {DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture)}");
        sb.AppendLine("# frequency is derived from count, first and last occurrence");
        sb.AppendLine();

        foreach (KeyValuePair<TelegramKey, TelegramStat> entry in
                 _stats.OrderByDescending(static x => x.Value.Count).ThenBy(static x => x.Value.Name))
        {
            TelegramStat stat = entry.Value;
            double spanHours = (stat.LastSeenUtc - stat.FirstSeenUtc).TotalHours;
            double ratePerHour = spanHours > 0 ? stat.Count / spanHours : 0;
            TimeSpan? averageInterval = stat.Count > 1
                ? TimeSpan.FromTicks((stat.LastSeenUtc - stat.FirstSeenUtc).Ticks / (stat.Count - 1))
                : null;
            string averageIntervalSeconds = averageInterval.HasValue
                ? averageInterval.Value.TotalSeconds.ToString("F1", CultureInfo.InvariantCulture)
                : "-";
            string averageIntervalText = averageInterval.HasValue
                ? FormatInterval(averageInterval.Value)
                : "-";

            sb.Append(HeaderKey);
            sb.Append("sender=").Append(stat.SenderCanId).Append(' ');
            sb.Append("sender_name=").Append(ModuleName(stat.SenderCanId)).Append(' ');
            sb.Append("receiver=").Append(stat.ReceiverCanId).Append(' ');
            sb.Append("receiver_name=").Append(ModuleName(stat.ReceiverCanId)).Append(' ');
            sb.Append("index=").Append(stat.ElsterIndex.ToString("X4")).Append(' ');
            sb.Append("name=").Append(stat.Name).Append(' ');
            sb.Append("type=").Append(stat.TelegramType).Append(' ');
            sb.Append("count=").Append(stat.Count).Append(' ');
            sb.Append("first=").Append(stat.FirstSeenUtc.ToString("O", CultureInfo.InvariantCulture)).Append(' ');
            sb.Append("last=").Append(stat.LastSeenUtc.ToString("O", CultureInfo.InvariantCulture)).Append(' ');
            sb.Append("span_h=").Append(spanHours.ToString("F1", CultureInfo.InvariantCulture)).Append(' ');
            sb.Append("rate_per_h=").Append(ratePerHour.ToString("F6", CultureInfo.InvariantCulture)).Append(' ');
            sb.Append("avg_interval_s=").Append(averageIntervalSeconds).Append(' ');
            sb.Append("avg_interval=").Append(averageIntervalText).Append(' ');
            sb.Append("last_hex=").Append(stat.LastValueHex).Append(' ');
            sb.Append("last_value=").Append(Sanitize(stat.LastValue));
            sb.AppendLine();

            foreach (ValueStat value in stat.Values.Values.OrderByDescending(static x => x.Count).ThenBy(static x => x.Hex))
            {
                sb.Append(HeaderVal);
                sb.Append("hex=").Append(value.Hex).Append(' ');
                sb.Append("count=").Append(value.Count).Append(' ');
                sb.Append("first=").Append(value.FirstSeenUtc.ToString("O", CultureInfo.InvariantCulture)).Append(' ');
                sb.Append("last=").Append(value.LastSeenUtc.ToString("O", CultureInfo.InvariantCulture)).Append(' ');
                sb.Append("value=").Append(Sanitize(value.Value));
                sb.AppendLine();
            }

            sb.AppendLine();
        }

        return sb.ToString();
    }

    private void Load()
    {
        try
        {
            string path = Path.GetFullPath(_config.OutputFile);
            if (!File.Exists(path)) return;

            TelegramStat? currentStat = null;
            foreach (string line in File.ReadLines(path))
            {
                if (line.StartsWith(HeaderKey, StringComparison.Ordinal))
                {
                    currentStat = ParseKeyLine(line[HeaderKey.Length..]);
                    if (currentStat != null)
                    {
                        TelegramKey key = new(currentStat.SenderCanId, currentStat.ReceiverCanId,
                                              currentStat.ElsterIndex, currentStat.TelegramType);
                        _stats[key] = currentStat;
                    }
                }
                else if (currentStat != null && line.StartsWith(HeaderVal, StringComparison.Ordinal))
                {
                    ValueStat? value = ParseValueLine(line[HeaderVal.Length..]);
                    if (value != null)
                    {
                        currentStat.Values[value.Hex] = value;
                    }
                }
            }

            _logger.LogInformation("Loaded {count} passive telegram types from {path}", _stats.Count, path);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not load passive telegram statistics from {path}", _config.OutputFile);
        }
    }

    private static TelegramStat? ParseKeyLine(string body)
    {
        int markerIndex = body.IndexOf(LastValueMarker, StringComparison.Ordinal);
        string head = markerIndex >= 0 ? body[..markerIndex] : body;
        string lastValue = markerIndex >= 0 ? body[(markerIndex + LastValueMarker.Length)..] : string.Empty;
        Dictionary<string, string> fields = ParseFields(head);

        if (!TryGetUInt(fields, "sender", out uint sender) ||
            !TryGetUInt(fields, "receiver", out uint receiver) ||
            !TryGetUShortHex(fields, "index", out ushort elsterIndex) ||
            !TryGetLong(fields, "count", out long count) ||
            !TryGetDateTime(fields, "first", out DateTime first) ||
            !TryGetDateTime(fields, "last", out DateTime last) ||
            !fields.TryGetValue("type", out string? typeText) ||
            !Enum.TryParse(typeText, true, out ElsterTelegramType telegramType))
        {
            return null;
        }

        return new TelegramStat
        {
            SenderCanId = sender,
            ReceiverCanId = receiver,
            ElsterIndex = elsterIndex,
            Name = fields.GetValueOrDefault("name", $"UNKNOWN_{elsterIndex:X4}"),
            TelegramType = telegramType,
            Count = count,
            FirstSeenUtc = first,
            LastSeenUtc = last,
            LastValueHex = fields.GetValueOrDefault("last_hex", string.Empty),
            LastValue = lastValue
        };
    }

    private static ValueStat? ParseValueLine(string body)
    {
        int markerIndex = body.IndexOf(ValueMarker, StringComparison.Ordinal);
        string head = markerIndex >= 0 ? body[..markerIndex] : body;
        string valueText = markerIndex >= 0 ? body[(markerIndex + ValueMarker.Length)..] : string.Empty;
        Dictionary<string, string> fields = ParseFields(head);

        if (!fields.TryGetValue("hex", out string? hex) ||
            !TryGetLong(fields, "count", out long count) ||
            !TryGetDateTime(fields, "first", out DateTime first) ||
            !TryGetDateTime(fields, "last", out DateTime last))
        {
            return null;
        }

        return new ValueStat
        {
            Hex = hex,
            Value = valueText,
            Count = count,
            FirstSeenUtc = first,
            LastSeenUtc = last
        };
    }

    private static Dictionary<string, string> ParseFields(string text)
    {
        Dictionary<string, string> fields = new(StringComparer.Ordinal);
        foreach (string token in text.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            int separator = token.IndexOf('=');
            if (separator <= 0) continue;
            fields[token[..separator]] = token[(separator + 1)..];
        }
        return fields;
    }

    private static bool TryGetUInt(Dictionary<string, string> fields, string name, out uint value)
    {
        value = 0;
        return fields.TryGetValue(name, out string? text) &&
               uint.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out value);
    }

    private static bool TryGetLong(Dictionary<string, string> fields, string name, out long value)
    {
        value = 0;
        return fields.TryGetValue(name, out string? text) &&
               long.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out value);
    }

    private static bool TryGetUShortHex(Dictionary<string, string> fields, string name, out ushort value)
    {
        value = 0;
        return fields.TryGetValue(name, out string? text) &&
               ushort.TryParse(text, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out value);
    }

    private static bool TryGetDateTime(Dictionary<string, string> fields, string name, out DateTime value)
    {
        value = default;
        if (!fields.TryGetValue(name, out string? text) ||
            !DateTime.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out DateTime parsed))
        {
            return false;
        }

        value = parsed.Kind == DateTimeKind.Utc ? parsed : parsed.ToUniversalTime();
        return true;
    }

    private static string ModuleName(uint canId)
    {
        return Enum.IsDefined(typeof(ElsterModule), (int)canId)
            ? ((ElsterModule)canId).ToString()
            : $"0x{canId:X3}";
    }

    private static string Sanitize(string value)
    {
        return value.Replace('\r', ' ').Replace('\n', ' ');
    }

    private static string FormatInterval(TimeSpan interval)
    {
        if (interval.TotalSeconds < 60)
        {
            return interval.TotalSeconds.ToString("F0", CultureInfo.InvariantCulture) + "s";
        }
        if (interval.TotalMinutes < 60)
        {
            return interval.TotalMinutes.ToString("F1", CultureInfo.InvariantCulture) + "min";
        }
        if (interval.TotalHours < 48)
        {
            return interval.TotalHours.ToString("F1", CultureInfo.InvariantCulture) + "h";
        }
        return interval.TotalDays.ToString("F1", CultureInfo.InvariantCulture) + "d";
    }

    private readonly record struct TelegramKey(uint SenderCanId,
                                               uint ReceiverCanId,
                                               ushort ElsterIndex,
                                               ElsterTelegramType TelegramType);

    private sealed class TelegramStat
    {
        public uint SenderCanId { get; init; }
        public uint ReceiverCanId { get; init; }
        public ushort ElsterIndex { get; init; }
        public string Name { get; init; } = string.Empty;
        public ElsterTelegramType TelegramType { get; init; }
        public long Count { get; set; }
        public DateTime FirstSeenUtc { get; init; }
        public DateTime LastSeenUtc { get; set; }
        public string LastValueHex { get; set; } = string.Empty;
        public string LastValue { get; set; } = string.Empty;
        public Dictionary<string, ValueStat> Values { get; } = new(StringComparer.Ordinal);
    }

    private sealed class ValueStat
    {
        public string Hex { get; init; } = string.Empty;
        public string Value { get; set; } = string.Empty;
        public long Count { get; set; }
        public DateTime FirstSeenUtc { get; init; }
        public DateTime LastSeenUtc { get; set; }
    }
}
