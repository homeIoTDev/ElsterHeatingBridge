using System;
using System.Collections.Generic;

namespace HeatingDaemon;

/// <summary>
/// Konfigurationsklasse für den HeatingMqttService.
/// </summary>
public class HeatingMqttServiceConfig
{
    /// <summary>
    /// Liste der zyklischen Leseabfragen.
    /// </summary>
    public List<CyclicReadingQueryConfig>? CyclicReadingsQuery { get; set; }
}
