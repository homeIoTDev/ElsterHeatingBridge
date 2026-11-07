using System.Runtime.InteropServices;
using HeatingDaemon.ElsterProtocol;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace HeatingDaemon;

/// <summary>
/// Writes the passive telegram statistics periodically, on shutdown and on SIGUSR1.
/// </summary>
public sealed class PassiveTelegramLogService : IHostedService, IDisposable
{
    private readonly PassiveTelegramLog _passiveTelegramLog;
    private readonly PassiveTelegramLogConfig _config;
    private readonly ILogger<PassiveTelegramLogService> _logger;

    private CancellationTokenSource? _cts;
    private Task? _worker;
    private PosixSignalRegistration? _signalRegistration;

    public PassiveTelegramLogService(PassiveTelegramLog passiveTelegramLog,
                                     IOptions<PassiveTelegramLogConfig> config,
                                     ILogger<PassiveTelegramLogService> logger)
    {
        _passiveTelegramLog = passiveTelegramLog;
        _config = config.Value;
        _logger = logger;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        if (!_config.Enabled)
        {
            _logger.LogInformation("Passive telegram statistics are disabled.");
            return Task.CompletedTask;
        }

        TimeSpan interval = GetFlushInterval();
        _logger.LogInformation("Starting passive telegram statistics (file={file}, interval={interval}, SIGUSR1={signal})",
                               _config.OutputFile, interval, _config.DumpOnSigusr1);

        if (_config.DumpOnSigusr1 && TryGetSigusr1(out PosixSignal sigusr1))
        {
            try
            {
                _signalRegistration = PosixSignalRegistration.Create(sigusr1, context =>
                {
                    context.Cancel = true;
                    _passiveTelegramLog.RequestFlush();
                });
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Could not register SIGUSR1 handler for passive telegram statistics.");
            }
        }

        _cts = new CancellationTokenSource();
        _worker = Task.Run(() => RunAsync(interval, _cts.Token));
        return Task.CompletedTask;
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        _cts?.Cancel();

        if (_worker != null)
        {
            try
            {
                await _worker.ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
            }
        }

        _signalRegistration?.Dispose();
        _signalRegistration = null;

        if (_config.Enabled)
        {
            _passiveTelegramLog.Flush();
        }
    }

    private async Task RunAsync(TimeSpan interval, CancellationToken token)
    {
        DateTime lastFlush = DateTime.UtcNow;

        while (!token.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(TimeSpan.FromSeconds(1), token).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                break;
            }

            if (_passiveTelegramLog.FlushRequested || (DateTime.UtcNow - lastFlush) >= interval)
            {
                _passiveTelegramLog.ClearFlushRequest();
                _passiveTelegramLog.Flush();
                lastFlush = DateTime.UtcNow;
            }
        }
    }

    private TimeSpan GetFlushInterval()
    {
        if (ElsterUserInputParser.ParseISO8601Timespan(_config.FlushInterval, out TimeSpan? duration) &&
            duration is TimeSpan interval && interval > TimeSpan.Zero)
        {
            return interval;
        }

        _logger.LogWarning("Invalid FlushInterval '{interval}'. Using 30 days.", _config.FlushInterval);
        return TimeSpan.FromDays(30);
    }

    /// <summary>
    /// The <see cref="PosixSignal"/> enum does not contain SIGUSR1, but any
    /// numeric signal can be registered. The value depends on the Unix platform.
    /// </summary>
    private static bool TryGetSigusr1(out PosixSignal signal)
    {
        int number;
        if (OperatingSystem.IsLinux())
        {
            number = 10; // SIGUSR1 on Linux
        }
        else if (OperatingSystem.IsMacOS() || OperatingSystem.IsFreeBSD())
        {
            number = 30; // SIGUSR1 on macOS/BSD
        }
        else
        {
            signal = default;
            return false;
        }

        signal = (PosixSignal)number;
        return true;
    }

    public void Dispose()
    {
        _cts?.Dispose();
        _signalRegistration?.Dispose();
    }
}
