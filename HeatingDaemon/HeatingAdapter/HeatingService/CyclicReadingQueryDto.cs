
namespace HeatingDaemon;

/// <summary>
/// Datenübertragungsobjekt für eine zyklische Leseabfrage.
/// </summary>
public class CyclicReadingQueryDto
{
    /// <summary>
    /// Name der Leseabfrage.
    /// </summary>
    public string ReadingName { get; set; }

    /// <summary>
    /// Sender CAN-ID.
    /// </summary>
    public ElsterModule SenderCanId { get; set; }

    /// <summary>
    /// Empfänger CAN-ID.
    /// </summary>
    public ElsterModule ReceiverCanId { get; set; }

    /// <summary>
    /// Funktion der Leseabfrage.
    /// </summary>
    public OperationType Operation { get; set; }

    /// <summary>
    /// Zeitplantyp der Leseabfrage, wenn null oder der Wert falsch geschrieben ist,
    /// wird <see cref="ScheduleType.Periodic"/> verwendet.
    /// </summary>
    public ScheduleType Schedule { get; set; }

    /// <summary>
    /// Intervall der Leseabfrage.
    /// </summary>
    public TimeSpan Interval { get; set; }

    /// <summary>
    /// Elster-Index für der Leseabfrage.
    /// Der Wert muss angegeben werden, wenn <see cref="Operation"/> <see cref="OperationType.GetElsterValue"/> ist.
    /// </summary>
    public ushort ElsterIndex { get; set; }

    /// <summary>
    /// Sendebedingung der Leseabfrage, wenn null oder der Wert falsch geschrieben ist,
    /// wird <see cref="SendCondition.OnValueChange"/> verwendet.
    /// </summary>
    public SendCondition SendCondition { get; set; }
    /// <summary>
    /// Zeitpunkt der letzten Leseabfrage, Standard ist <see cref="DateTime.MinValue"/>
    /// </summary>
    public DateTime LastReadTime { get; set; } = DateTime.MinValue;

 
    /// <summary>
    /// Konstruktor der <see cref="CyclicReadingQueryDto"/> Klasse, wird nur intern 
    /// verwendet von <see cref="From(CyclicReadingQueryConfig)"/>
    /// </summary>
    /// <param name="readingName">Name der Leseabfrage.</param>
    public CyclicReadingQueryDto(string readingName) {
        ReadingName = readingName;
    }
    /// <summary>
    /// Gibt die Repräsentation des Objekts als Zeichenfolge zurück.
    /// </summary>
    /// <returns></returns>
    public override string ToString()
    {
        return $"ReadingName: {ReadingName}, SenderCanID: {SenderCanId}, ReceiverCanID: {ReceiverCanId}, Operation: {Operation}, Schedule: {Schedule}, Interval: {Interval}, ElsterIndex: {ElsterIndex}, SendCondition: {SendCondition}";
    }
}
