using System;

namespace HeatingDaemon;

public class HeatingAdapterConfig
{
    /// <summary>
    /// Can-ID, die für das Senden genutzt wird, wenn keine ID explizit angegeben wird. Das Format ist hexadezimal, z.B. 0x700
    /// Mögliche SenderCanIDs: 0x700 (External), 0x710 to 0x71f, and 0x780 to 0x79f, 0x680 to ox69f
    /// </summary>
    public string StandardSenderCanID { get; set; } = "0x700";

    /// <summary>
    /// Anzahl der Wiederholungen bei einem Sendefehler
    /// </summary>
    public int SendRetryCount { get; set; } = 3;
    /// <summary>
    /// Verzögerung in ms zwischen den Wiederholungen
    /// </summary>
    public int SendRetryDelay { get; set; } = 100;
    /// <summary>
    /// Maximale Wartezeit in ms für den Empfang eines Antwortframes
    /// </summary>
    public int MaxReceivingWaitTime { get; set; } = 560; 
}
