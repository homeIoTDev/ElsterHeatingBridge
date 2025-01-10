namespace HeatingDaemon;

/// <summary>
/// Zeitplantypen für zyklische Leseabfragen.
/// </summary>
public enum ScheduleType
{
    /// <summary>
    /// Die Leseabfrage wird beim Start der Anwendung einamlig ausgeführt.
    /// </summary>
    AtStartup,
    /// <summary>
    /// Die Leseabfrage wird periodisch ausgeführt. Ein Intervall in Sekunden ist erforderlich.
    /// </summary>
    Periodic,
    /// <summary>
    /// Die Leseabfrage wird nicht ausgeführt, sondern nur 
    /// Telegramme, die durch anderer Busteilnehmen erzeugt wurden,
    /// werden verarbeitet.
    /// </summary>
    Passive 
}
