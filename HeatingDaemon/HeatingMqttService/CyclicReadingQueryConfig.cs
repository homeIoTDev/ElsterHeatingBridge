using System.Globalization;
namespace HeatingDaemon;

/// <summary>
/// Konfigurationsklasse für eine zyklische Leseabfrage.
/// </summary>
public class CyclicReadingQueryConfig
{
    /// <summary>
    /// Name der Leseabfrage.
    /// </summary>
    public required string ReadingName { get; set; }

    /// <summary>
    /// Sender CAN-ID oder wenn null, wird die Standard-CAN-ID verwendet.
    /// Die CAN-ID kann als Hexadezimalzahl angegeben werden in der Form "0x123" oder "123" oder
    /// ein Wert aus <see cref="ElsterModule"/> wie z.B. <see cref="ElsterModule.RemoteControl"/>
    /// </summary>
    public string? SenderCanID { get; set; }

    /// <summary>
    /// Empfänger CAN-ID. Der Wert darf nicht leer sein.
    /// Die CAN-ID kann als Hexadezimalzahl angegeben werden in der Form "0x123" oder "123" oder
    /// ein Wert aus <see cref="ElsterModule"/> wie z.B. <see cref="ElsterModule.RemoteControl"/>
    /// </summary>
    public required string ReceiverCanID { get; set; }

    /// <summary>
    /// Operation fürder Leseabfrage. Standardwert ist "GetElsterValue", wenn nichts angegeben 
    /// ist.
    /// </summary>
    public string? Operation { get; set; }

    /// <summary>
    /// Zeitplantyp der Leseabfrage.
    /// </summary>
    public string? ScheduleType { get; set; }

    /// <summary>
    /// Intervall in Sekunden. Muss nur angegeben werden, wenn <see cref="ScheduleType"/>
    /// <see cref="ScheduleType.Periodic"/> ist.
    /// </summary>
    public int IntervalInSeconds { get; set; }

    /// <summary>
    /// Elster-Index für die Leseabfrage. Der Wert muss angegeben werden, wenn <see cref="Operation"/>
    /// <see cref="OperationType.GetElsterValue"/> ist. Der Elster Index kann z.B. "0x123" oder "123" sein.
    /// Er kann aber auch ein Wert aus <see cref="ElsterTable"/> sein.
    /// </summary>
    public string? ElsterIndex { get; set; }

    /// <summary>
    /// Sendebedingung der Leseabfrage.
    /// </summary>
    public string? SendCondition { get; set; }

       /// <summary>
    /// Erstellt eine Instanz der <see cref="CyclicReadingQueryDto"/> Klasse aus der Konfiguration.
    /// </summary>
    /// <param name="config">Die Konfiguration der zyklischen Leseabfrage.</param>
    /// <returns>Eine Instanz von <see cref="CyclicReadingQueryDto"/> oder <c>null</c>, wenn die Interpretation fehlschlägt.</returns>
    public CyclicReadingQueryDto? ToCyclicReadingQueryDto()
    {
        //Es muss ein gültiger ReadingName und ein Receiver-Can-ID vorliegen
        if( string.IsNullOrWhiteSpace(this.ReadingName)||
            string.IsNullOrWhiteSpace(this.ReceiverCanID))
        {
            return null;
        }

        if (!Enum.TryParse<ScheduleType>(this.ScheduleType, out var scheduleType))
        {
            scheduleType = HeatingDaemon.ScheduleType.Periodic;
        }

        if (!Enum.TryParse<SendCondition>(this.SendCondition, out var sendCondition))
        {
            sendCondition = HeatingDaemon.SendCondition.OnValueChange;
        }

        if (!Enum.TryParse<OperationType>(this.SendCondition, out var operation))
        {
            operation = OperationType.GetElsterValue;
        }

        ElsterModule senderCanID;
        ushort canId;
        if (string.IsNullOrWhiteSpace(this.SenderCanID))
        {
            senderCanID = (ElsterModule)0xFFF;  // 0xFFF ist ungültig und wird beim Senden durch die Standard-CAN-ID ersetzt 
        }
        else if (ushort.TryParse(this.SenderCanID.Replace("0x", ""), NumberStyles.HexNumber, null, out canId))
        {
            senderCanID = (ElsterModule)canId;
        } 
        else if (Enum.TryParse<ElsterModule>(this.SenderCanID, out var elsterModule))
        {
            senderCanID = elsterModule;
        }
        else // Weder ElsterModule-Name noch eine Hexzahl, dann Standard-CAN-ID
        {
            senderCanID = (ElsterModule)0xFFF;  // 0xFFF ist ungültig und wird beim Senden durch die Standard-CAN-ID ersetzt 
        }

        ElsterModule receiverCanID;
        if (ushort.TryParse(this.ReceiverCanID.Replace("0x", ""), NumberStyles.HexNumber, null, out canId))
        {
            receiverCanID = (ElsterModule)canId;
        } 
        else if (Enum.TryParse<ElsterModule>(this.ReceiverCanID, out var elsterModule))
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
            if(string.IsNullOrWhiteSpace(this.ElsterIndex) )
            {
                return null;
            }
            else if (ushort.TryParse(this.ElsterIndex.Replace("0x", ""), NumberStyles.HexNumber, null, out var elsterIndexHex))
            {
                elsterIndex = elsterIndexHex;
            } 
            else if (KElsterTable.ElsterTabIndexName.TryGetValue(this.ElsterIndex, out var elsterIndexName))
            {
                elsterIndex = elsterIndexName;
            }
            else // Weder Elster-Index-Name noch eine Hexzahl, dann geht es nicht
            {
                return null;
            }
        }

        return new CyclicReadingQueryDto ( this.ReadingName)
        {
            SenderCanID     = senderCanID,
            ReceiverCanID   = receiverCanID,
            Operation       = operation,
            Schedule        = scheduleType,
            Interval        = TimeSpan.FromSeconds(this.IntervalInSeconds),
            ElsterIndex     = elsterIndex,
            SendCondition   = sendCondition
        };

    }


    public override string ToString()
    {
        return $"{ReadingName} {SenderCanID} {ReceiverCanID} {Operation} {ScheduleType} {IntervalInSeconds} {ElsterIndex} {SendCondition}";
    }
}
