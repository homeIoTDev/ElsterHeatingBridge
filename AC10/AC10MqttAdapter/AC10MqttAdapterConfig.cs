using System;

namespace AC10Service;

/// <summary>
/// Diese KLasse stellt die Konfiguration für den MQTT-Adapter dar. Diese Einstellungen
/// werden genutzt um eine MQTT-Verbindung z.B. zu einem FHEM-Server (mit MQTT-Broker) aufzubauen.
/// </summary>
public class AC10MqttAdapterConfig
{
    /// <summary>
    /// The Uri of the MQTT Server. Example: mqtt://user:psw@localhost:1883
    /// </summary>
    public string ServerUri { get; set; } = "mqtt://user:psw@localhost:1883";

    /// <summary>
    /// The ClientId to use for the connection to the MQTT Server
    /// </summary>
    public string ClientId { get; set; } = "AC10HeatingMqttService";

    /// <summary>
    /// The Topic to use for the MQTT messages
    /// </summary>
    public string Topic { get; set; } = "AC10"; 
}
