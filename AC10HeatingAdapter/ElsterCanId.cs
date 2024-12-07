using System;

namespace AC10Service;

/// <summary>
/// CAN-IDs der Elster-Systeme.
/// </summary>
/// <remarks>
/// https://knx-user-forum.de/forum/öffentlicher-bereich/knx-eib-forum/code-schnipsel/26505-anbindung-tecalor-ttw13?p=634595#post634595
/// </remarks>
public enum ElsterModule
{
    /// <summary>
    /// CAN-ID 0x0300 - Direct
    /// </summary>
    Direct = 0x0000,
    /// <summary>
    /// CAN-ID 0x0100 - Unknown module in Tecalor WPL 10 AC mit FEK, Puffer, WPM3
    /// </summary>
    Unknown_100h = 0x0100,
    /// <summary>
    /// CAN-ID 0x0180 - Boiler module
    /// </summary>
    Boiler = 0x0180,
    /// <summary>
    /// CAN-ID 0x0280 - Atez module
    /// </summary>
    AtezModule = 0x280,
    /// <summary>
    /// CAN-ID 0x0301 - Remote control module (FEK)
    /// </summary>
    RemoteControl = 0x301,
    /// <summary>
    /// CAN-ID 0x0302 - Remote control module 2
    /// </summary>
    RemoteControl2 = 0x302,
    /// <summary>
    /// CAN-ID 0x0303 - Remote control module 3
    /// </summary>
    RemoteControl3 = 0x303,
    /// <summary>
    /// CAN-ID 0x0400 - Room thermostat
    /// </summary>
    RoomThermostat = 0x400,
    /// <summary>
    /// CAN-ID 0x0480 - Manager
    /// </summary>
    Manager = 0x480,
    /// <summary>
    /// CAN-ID 0x0500 - Electric Heater Control Module
    /// </summary>
    HeatingModule = 0x500,
    /// <summary>
    /// CAN-ID 0x0580 - Bus coupler
    /// </summary>
    BusCoupler = 0x580,
    /// <summary>
    /// CAN-ID 0x0601 - Mixer module 1
    /// </summary>
    Mixer = 0x601,
    /// <summary>
    /// CAN-ID 0x0602 - Mixer module 2
    /// </summary>
    Mixer2 = 0x602,
    /// <summary>
    /// CAN-ID 0x0603 - Mixer module 3
    /// </summary>
    Mixer3 = 0x603,
    /// <summary>
    /// CAN-ID 0x0680 - PC (ComfortSoft). Don't use as sender id with WPM3.
    /// </summary>
    ComfortSoft = 0x680,
    /// <summary>
    /// CAN-ID 0x0700 - External device
    /// </summary>
    ExternalDevice = 0x700,
    /// <summary>
    /// CAN-ID 0x0780 - DCF module
    /// </summary>
    Dcf = 0x780
}
