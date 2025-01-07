using System;
using System.Collections.Generic;

namespace HeatingDaemon;


public class HeatingMqttServiceConfig
{
    public List<CyclicReadingQuery>? CyclicReadingsQuery { get; set; }
}

public class CyclicReadingQuery
{
    public required string ReadingName { get; set; }
    public string? SenderCanID { get; set; }
    public required string ReceiverCanID { get; set; }
    public string? Function { get; set; }
    public string? ScheduleType { get; set; }
    public int IntervalInSeconds { get; set; }
    public string? ElsterValue { get; set; }
}