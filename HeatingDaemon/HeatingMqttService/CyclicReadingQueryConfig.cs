using System.Globalization;
using HeatingDaemon.ElsterProtocol;
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

        ElsterModule senderCanID = ElsterUserInputParser.ParseSenderElsterModule(this.SenderCanID);

        ElsterModule receiverCanID;
        var receiverResult = ElsterUserInputParser.ParseReceiverElsterModule(this.ReceiverCanID);
        if (receiverResult == null)
        {
            return null;
        }
        receiverCanID = receiverResult.Value;

        ushort elsterIndex = 0;
        if(operation == OperationType.GetElsterValue)
        {
            var elsterIndexResult = ElsterUserInputParser.ParseElsterIndex(this.ElsterIndex);
            if (elsterIndexResult == null)
            {
                return null;
            }
            elsterIndex = elsterIndexResult.Value;
        }

        return new CyclicReadingQueryDto ( this.ReadingName)
        {
            SenderCanId     = senderCanID,
            ReceiverCanId   = receiverCanID,
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
