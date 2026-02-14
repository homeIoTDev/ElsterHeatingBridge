namespace HeatingDaemon;

/// <summary>
/// Konfiguration für die Auswahl des CAN-Bus Adapters (USBtin vs. SocketCAN).
/// </summary>
public class CanBusAdapterConfig
{
    /// <summary>
    /// Auswahl des Adapters. Werte: UsbTin | SocketCan
    /// </summary>
    public CanBusAdapterType AdapterType { get; set; } = CanBusAdapterType.UsbTin;

    /// <summary>
    /// Name des SocketCAN Interfaces (z.B. "can0" oder "vcan0"). Wird nur verwendet, wenn AdapterType=SocketCan.
    /// </summary>
    public string SocketCanInterfaceName { get; set; } = "can0";

    /// <summary>
    /// ReceiveTimeout für RawCanSocket.Read() in Millisekunden. Dadurch kann die Read-Loop regelmäßig auf Stop/Cancellation prüfen.
    /// Wird nur verwendet, wenn AdapterType=SocketCan.
    /// </summary>
    public int SocketCanReceiveTimeoutMs { get; set; } = 1000;
}

public enum CanBusAdapterType
{
    UsbTin = 0,
    SocketCan = 1
}
