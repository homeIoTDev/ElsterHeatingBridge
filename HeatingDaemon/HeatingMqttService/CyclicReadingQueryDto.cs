using System.Globalization;

namespace HeatingDaemon;

/// <summary>
/// Datenübertragungsobjekt für eine zyklische Leseabfrage.
/// </summary>
public class CyclicReadingQueryDto
{
    /// <summary>
    /// Name der Leseabfrage.
    /// </summary>
    public string ReadingName { get; private set; }

    /// <summary>
    /// Sender CAN-ID.
    /// </summary>
    public ElsterModule SenderCanID { get; private set; }

    /// <summary>
    /// Empfänger CAN-ID.
    /// </summary>
    public ElsterModule ReceiverCanID { get; private set; }

    /// <summary>
    /// Funktion der Leseabfrage.
    /// </summary>
    public OperationType Operation { get; private set; }

    /// <summary>
    /// Zeitplantyp der Leseabfrage, wenn null oder der Wert falsch geschrieben ist,
    /// wird <see cref="ScheduleType.Periodic"/> verwendet.
    /// </summary>
    public ScheduleType Schedule { get; private set; }

    /// <summary>
    /// Intervall der Leseabfrage.
    /// </summary>
    public TimeSpan Interval { get; private set; }

    /// <summary>
    /// Elster-Index für der Leseabfrage.
    /// Der Wert muss angegeben werden, wenn <see cref="Operation"/> <see cref="OperationType.GetElsterValue"/> ist.
    /// </summary>
    public ushort ElsterIndex { get; private set; }

    /// <summary>
    /// Sendebedingung der Leseabfrage, wenn null oder der Wert falsch geschrieben ist,
    /// wird <see cref="SendCondition.OnValueChange"/> verwendet.
    /// </summary>
    public SendCondition SendCondition { get; private set; }

    /// <summary>
    /// Erstellt eine Instanz der <see cref="CyclicReadingQueryDto"/> Klasse aus der Konfiguration.
    /// </summary>
    /// <param name="config">Die Konfiguration der zyklischen Leseabfrage.</param>
    /// <returns>Eine Instanz von <see cref="CyclicReadingQueryDto"/> oder <c>null</c>, wenn die Interpretation fehlschlägt.</returns>
    public static CyclicReadingQueryDto? From(CyclicReadingQueryConfig config)
    {
        //Es muss ein gültiger ReadingName und ein Receiver-Can-ID vorliegen
        if( string.IsNullOrWhiteSpace(config.ReadingName)||
            string.IsNullOrWhiteSpace(config.ReceiverCanID))
        {
            return null;
        }

        if (!Enum.TryParse<ScheduleType>(config.ScheduleType, out var scheduleType))
        {
            scheduleType = ScheduleType.Periodic;
        }

        if (!Enum.TryParse<SendCondition>(config.SendCondition, out var sendCondition))
        {
            sendCondition = SendCondition.OnValueChange;
        }

        if (!Enum.TryParse<OperationType>(config.SendCondition, out var operation))
        {
            operation = OperationType.GetElsterValue;
        }

        ElsterModule senderCanID;
        ushort canId;
        if (string.IsNullOrWhiteSpace(config.SenderCanID))
        {
            senderCanID = (ElsterModule)0xFFF;  // 0xFFF ist ungültig und wird beim Senden durch die Standard-CAN-ID ersetzt 
        }
        else if (ushort.TryParse(config.SenderCanID, NumberStyles.HexNumber, null, out canId))
        {
            senderCanID = (ElsterModule)canId;
        } 
        else if (Enum.TryParse<ElsterModule>(config.SenderCanID, out var elsterModule))
        {
            senderCanID = elsterModule;
        }
        else // Weder ElsterModule-Name noch eine Hexzahl, dann Standard-CAN-ID
        {
            senderCanID = (ElsterModule)0xFFF;  // 0xFFF ist ungültig und wird beim Senden durch die Standard-CAN-ID ersetzt 
        }

        ElsterModule receiverCanID;
        if (ushort.TryParse(config.ReceiverCanID, NumberStyles.HexNumber, null, out canId))
        {
            receiverCanID = (ElsterModule)canId;
        } 
        else if (Enum.TryParse<ElsterModule>(config.ReceiverCanID, out var elsterModule))
        {
            receiverCanID = elsterModule;
        }
        else // Weder ElsterModule-Name noch eine Hexzahl, dann geht es nicht
        {
            return null;
        }

        ushort elsterIndex = 0;
        if(operation == OperationType.GetElsterValue)
        {
            if(string.IsNullOrWhiteSpace(config.ElsterIndex) )
            {
                return null;
            }
            else if (ushort.TryParse(config.ElsterIndex, NumberStyles.HexNumber, null, out var elsterIndexHex))
            {
                elsterIndex = elsterIndexHex;
            } 
            else if (KElsterTable.ElsterTabIndexName.TryGetValue(config.ElsterIndex, out var elsterIndexName))
            {
                elsterIndex = elsterIndexName;
            }
            else // Weder Elster-Index-Name noch eine Hexzahl, dann geht es nicht
            {
                return null;
            }
        }

        return new CyclicReadingQueryDto ( config.ReadingName)
        {
            SenderCanID     = senderCanID,
            ReceiverCanID   = receiverCanID,
            Operation       = operation,
            Schedule        = scheduleType,
            Interval        = TimeSpan.FromSeconds(config.IntervalInSeconds),
            ElsterIndex     = elsterIndex,
            SendCondition   = sendCondition
        };

    }

    /// <summary>
    /// Konstruktor der <see cref="CyclicReadingQueryDto"/> Klasse, wird nur intern 
    /// verwendet von <see cref="From(CyclicReadingQueryConfig)"/>
    /// </summary>
    /// <param name="readingName">Name der Leseabfrage.</param>
    private CyclicReadingQueryDto(string readingName) {
        ReadingName = readingName;
    }
    /// <summary>
    /// Gibt die Repräsentation des Objekts als Zeichenfolge zurück.
    /// </summary>
    /// <returns></returns>
    public override string ToString()
    {
        return $"ReadingName: {ReadingName}, SenderCanID: {SenderCanID}, ReceiverCanID: {ReceiverCanID}, Operation: {Operation}, Schedule: {Schedule}, Interval: {Interval}, ElsterIndex: {ElsterIndex}, SendCondition: {SendCondition}";
    }
}
