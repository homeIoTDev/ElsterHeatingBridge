namespace HeatingDaemon;

/// <summary>
/// Sendebedingungen für zyklische Leseabfragen.
/// </summary>
public enum SendCondition
{
    /// <summary>
    /// Bei jedem Lesen, wird ein Wert an die MQTT-Bridge geschickt.
    /// </summary>
    OnEveryRead,
    /// <summary>
    /// Nur bei Wertänderung, wird ein Wert an die MQTT-Bridge geschickt.
    /// </summary>
    OnValueChange
}
