using System.Net.Sockets;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SocketCANSharp.Network;

namespace HeatingDaemon;

/// <summary>
/// CAN-Bus Adapter basierend auf Linux SocketCAN (z.B. can0 / vcan0).
/// Nutzt die NuGet-Library "SocketCANSharp" (RawCanSocket).
/// </summary>
public class SocketCanCanBusAdapter : IDisposable, ICanBusService
{
    // Reconnect Interval analog zu UsbTinCanBusAdapter
    private static readonly TimeSpan _OpenCheckTimeInterval = TimeSpan.FromMilliseconds(10000);

    // SocketCAN Flag-Bits im can_id (Linux)
    private const uint CAN_EFF_FLAG = 0x80000000; // Extended Frame Format
    private const uint CAN_RTR_FLAG = 0x40000000; // Remote Transmission Request
    private const uint CAN_ERR_FLAG = 0x20000000; // Error Frame

    private const uint CAN_EFF_MASK = 0x1FFFFFFF; // 29-bit identifier
    private const uint CAN_SFF_MASK = 0x000007FF; // 11-bit identifier

    private readonly ILogger<SocketCanCanBusAdapter> _logger;
    private readonly CanBusAdapterConfig _config;
    private readonly Lazy<IHeatingService> _heatingService;
    private readonly IMqttService _mqttService;

    private readonly object _socketLock = new();

    private RawCanSocket? _rawCanSocket;
    private CancellationTokenSource? _readCts;
    private Task? _readTask;
    private System.Timers.Timer? _openSocketTimer;

    public bool IsCanBusOpen { get; private set; }

    public SocketCanCanBusAdapter(
        IOptions<CanBusAdapterConfig> config,
        Lazy<IHeatingService> heatingService,
        IMqttService mqttService,
        ILoggerFactory loggerFactory)
    {
        _logger         = loggerFactory.CreateLogger<SocketCanCanBusAdapter>();
        _config         = config.Value;
        _heatingService = heatingService;
        _mqttService    = mqttService;

        _mqttService.SetReading("CAN_Channel", "undefined");
    }

    public void Start()
    {
        _logger.LogInformation(
            "Starting SocketCanCanBusAdapter for interface '{iface}' (ReceiveTimeout={timeout}ms)...",
            _config.SocketCanInterfaceName,
            _config.SocketCanReceiveTimeoutMs);

        if (!OperatingSystem.IsLinux())
        {
            _logger.LogError("SocketCAN is only available on Linux. Current OS: {os}", Environment.OSVersion);
            return;
        }

        _openSocketTimer ??= new System.Timers.Timer(_OpenCheckTimeInterval)
        {
            AutoReset = true
        };

        _openSocketTimer.Elapsed += (sender, e) => TryOpenSocket();
        _openSocketTimer.Start();

        // Sofort versuchen, nicht erst nach 10s
        TryOpenSocket();
    }

    public void Stop()
    {
        try
        {
            _logger.LogInformation("SocketCanCanBusAdapter stopping...");
            _openSocketTimer?.Stop();
            _openSocketTimer?.Dispose();
            _openSocketTimer = null;

            CloseSocket();
            _mqttService.SetReading("CAN_Channel", "undefined", true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error while stopping SocketCanCanBusAdapter.");
        }
    }

    public void Reset()
    {
        _logger.LogInformation("Resetting SocketCanCanBusAdapter.");
        CloseSocket();
        IsCanBusOpen = false;
        _mqttService.SetReading("CAN_Channel", "undefined", true);
    }

    public bool SendCanFrame(CanFrame frame)
    {
        if (!IsCanBusOpen)
        {
            _logger.LogWarning("SocketCAN bus not open; can't send frame.");
            return false;
        }

        try
        {
            uint canId = BuildSocketCanId(frame);
            byte[] data = frame.Data ?? Array.Empty<byte>();

            lock (_socketLock)
            {
                if (_rawCanSocket == null)
                    return false;

                int bytesWritten = _rawCanSocket.Write(new SocketCANSharp.CanFrame(canId, data));
                return bytesWritten > 0;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending CAN frame via SocketCAN. Resetting adapter to trigger reconnect.");
            Reset();
            return false;
        }
    }

    private uint BuildSocketCanId(CanFrame frame)
    {
        // SocketCAN erwartet Flag-Bits im can_id, u.a. CAN_EFF_FLAG für Extended IDs.
        uint rawId = frame.SenderCanId;

        bool isExtended = frame is ExtendedCanFrame || rawId > CAN_SFF_MASK;
        if (isExtended)
            rawId |= CAN_EFF_FLAG;

        return rawId;
    }

    private void TryOpenSocket()
    {
        if (IsCanBusOpen)
            return;

        if (!OperatingSystem.IsLinux())
            return;

        try
        {
            string ifaceName = (_config.SocketCanInterfaceName ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(ifaceName))
            {
                _logger.LogError("SocketCAN interface name is empty. Configure CanBusAdapterConfig.SocketCanInterfaceName.");
                return;
            }

            // SocketCANSharp: Interface finden und Raw Socket binden
            var iface = CanNetworkInterface.GetAllInterfaces(true)
                .FirstOrDefault(i => i.Name.Equals(ifaceName, StringComparison.Ordinal));

            if (iface == null)
            {
                _logger.LogWarning("SocketCAN interface '{iface}' not found. Will retry.", ifaceName);
                return;
            }

            lock (_socketLock)
            {
                if (_rawCanSocket != null)
                    return;

                var sock = new RawCanSocket
                {
                    ReceiveTimeout = Math.Max(50, _config.SocketCanReceiveTimeoutMs)
                };

                // Default in SocketCANSharp: LocalLoopback=true, ReceiveOwnMessages=false
                // => Dieser Socket empfängt seine eigenen Frames NICHT.
                sock.Bind(iface);

                _rawCanSocket = sock;
            }

            IsCanBusOpen = true;
            _mqttService.SetReading("CAN_Channel", $"socketcan:{ifaceName} opened", true);

            _readCts  = new CancellationTokenSource();
            _readTask = Task.Run(() => ReadLoop(_readCts.Token), _readCts.Token);

            _logger.LogInformation("SocketCAN interface '{iface}' bound successfully.", ifaceName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to open SocketCAN interface. Will retry.");
            Reset();
        }
    }

    private void ReadLoop(CancellationToken token)
    {
        _logger.LogInformation("SocketCAN read loop started.");

        while (!token.IsCancellationRequested)
        {
            RawCanSocket? sock;
            lock (_socketLock)
            {
                sock = _rawCanSocket;
            }

            if (sock == null)
            {
                Thread.Sleep(200);
                continue;
            }

            try
            {
                int bytesRead = sock.Read(out SocketCANSharp.CanFrame rawFrame);
                if (bytesRead <= 0)
                    continue;

                // Error Frames ignorieren (optional: in MQTT/Logging ausgeben)
                if ((rawFrame.CanId & CAN_ERR_FLAG) != 0)
                {
                    _logger.LogWarning("Received CAN error frame: CanId=0x{canId:X8}.", rawFrame.CanId);
                    continue;
                }

                // RTR Frames werden in diesem Projekt aktuell nicht genutzt; ignorieren
                if ((rawFrame.CanId & CAN_RTR_FLAG) != 0)
                {
                    _logger.LogDebug("Ignoring RTR frame: CanId=0x{canId:X8}.", rawFrame.CanId);
                    continue;
                }

                CanFrame converted = ConvertToInternalFrame(rawFrame);
                _heatingService.Value.ProcessCanFrame(converted);
            }
            catch (SocketCanException ex) when (ex.SocketErrorCode == SocketError.WouldBlock)
            {
                // Timeout -> einfach weiter, damit Cancellation geprüft werden kann
                continue;
            }
            catch (ObjectDisposedException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in SocketCAN read loop; resetting adapter to trigger reconnect.");
                Reset();
                Thread.Sleep(1000);
            }
        }

        _logger.LogInformation("SocketCAN read loop ended.");
    }

    private static CanFrame ConvertToInternalFrame(SocketCANSharp.CanFrame rawFrame)
    {
        uint canId = rawFrame.CanId;
        bool isExtended = (canId & CAN_EFF_FLAG) != 0;

        uint arbitrationId = isExtended ? (canId & CAN_EFF_MASK) : (canId & CAN_SFF_MASK);

        int length = rawFrame.Length;
        if (length < 0) length = 0;
        if (length > 8) length = 8;

        byte[] data = rawFrame.Data ?? Array.Empty<byte>();
        if (data.Length > length)
            data = data.Take(length).ToArray();

        return isExtended
            ? new ExtendedCanFrame(arbitrationId, data)
            : new StandardCanFrame(arbitrationId, data);
    }

    private void CloseSocket()
    {
        try { _readCts?.Cancel(); } catch { }

        try
        {
            if (_readTask != null)
                _readTask.Wait(TimeSpan.FromSeconds(2));
        }
        catch { /* ignore */ }

        _readTask = null;

        try { _readCts?.Dispose(); } catch { }
        _readCts = null;

        lock (_socketLock)
        {
            try { _rawCanSocket?.Dispose(); } catch { }
            _rawCanSocket = null;
        }

        IsCanBusOpen = false;
    }

    public void Dispose()
    {
        Stop();
    }
}
