using System;

namespace AC10Service;

public class AC10HeatingAdapterConfig
{
    /// <summary>
    /// Mögliche SenderCanIDs: 700 (External), 710 to 71f, and 780 to 79f, 680 to 69f
    /// </summary>
    public ushort StandardSenderCanID { get; set; } = 0x700;
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
    public int MaxReceivingWaitTime { get; set; } = 400; 
}
