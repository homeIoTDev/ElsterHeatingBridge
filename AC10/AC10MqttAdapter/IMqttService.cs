using System;

namespace AC10Service;

public interface IMqttService
{
    /// <summary>
    /// Setzt den Wert einer Heizungsvariable. Der Wert wird im MQTT-Adapter zwischengespeichert und 
    /// erst dann an den MQTT-Broker gesendet, wenn sich der Wert aendert.
    /// </summary>
    /// <param name="name">Name der Heizungsvariable</param>
    /// <param name="value">Neuer Wert der Heizungsvariable</param>
    void SetReading(string name, string value);

    void LogAllReadings();
}
