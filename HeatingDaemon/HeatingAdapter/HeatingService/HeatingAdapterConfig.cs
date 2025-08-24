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
    public int SendRetryDelay { get; set; } = 300;

    /// <summary>
    /// Maximale Wartezeit in ms für den Empfang eines Antwortframes
    /// </summary>
    public int MaxReceivingWaitTime { get; set; } = 500;

    /// <summary>
    /// Maximale Anzahl an Telegrammen, die pro Zeitfenster von 250ms erwartet werden   
    /// </summary>
    public int MaxExpectedTelegrams { get; set; } = 40; // Erwartete max. Anzahl pro Fenster ( CAN-Bitrate: 20.000 Bits/s ÷ 120 Bits ≈ 166,6 Telegramme/s ) ca. 40 Telegramme pro 250 ms

    /// <summary>
    /// Wartezeit in ms zwischen zwei aufeinanderfolgenden Sendungen, um bestimmte Busteilnehmer zu entlasten
    /// (insbesondere bei mehrfachen Abfragen desselben Wertes)
    /// </summary>
    //ToDo: Evtl. diese Zeitwert für SendRetryDelay verwenden (die zeit bezieht sich auf einenen Bus-Teilnehmer!)
    public int MinTimeBetweenSendsMs { get; set; } = 600;

    /// <summary>
    /// Minimale Buslast, aber der gesendet werden darf. Es wird solange gewartet, bis die Buslast wieder unter diesem Wert liegt.
    /// Angabe in Prozent (0-100). Standardwert ist 11%, da bei 10% Buslast der Bus schon sehr voll ist in 250ms Zeitfenster.
    /// </summary>
    public int MinBusLoadPercentage { get; set; } = 11; 
    
    /// <summary>
    /// Maximale Wartezeit in ms, bis trotz hoher Buslast gesendet wird.
    /// </summary>
    public int MaxBusLoadWaitTimeMs { get; set; } = 2000;
}
