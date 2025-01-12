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

    public override string ToString()
    {
        return $"{ReadingName} {SenderCanID} {ReceiverCanID} {Operation} {ScheduleType} {IntervalInSeconds} {ElsterIndex} {SendCondition}";
    }
}
