namespace HeatingDaemon;

public interface ICanBusService
{
    /// <summary>
    /// Startet den CAN-Bus Adapter (öffnet Port/Socket und startet die Empfangs-Loop).
    /// </summary>
    void Start();

    /// <summary>
    /// Stoppt den CAN-Bus Adapter (schließt Port/Socket und beendet die Empfangs-Loop).
    /// </summary>
    void Stop();

    /// <summary>
    /// Setzt den Adapter zurück (schließt Port/Socket und triggert Reconnect-Mechanismen).
    /// </summary>
    void Reset();

    /// <summary>
    /// Sendet ein CAN-Frame auf den Bus.
    /// </summary>
    bool SendCanFrame(CanFrame frame);

    /// <summary>
    /// True wenn die physische/virtuelle CAN-Verbindung offen ist.
    /// </summary>
    bool IsCanBusOpen { get; }
}
