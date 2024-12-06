using System;

namespace AC10Service;

/// <summary>
/// CAN-IDs der Elster-Systeme.
/// </summary>
/// <remarks>
/// https://knx-user-forum.de/forum/öffentlicher-bereich/knx-eib-forum/code-schnipsel/26505-anbindung-tecalor-ttw13?p=634595#post634595
/// </remarks>
public enum ElsterCanId
{
    /// <summary>
    /// CAN-ID 0x0300 - Direct
    /// </summary>
    Direct = 0x0000,
    /// <summary>
    /// CAN-ID 0x0180 - Boiler module
    /// </summary>
    BoilerModule = 0x0180,
    /// <summary>
    /// CAN-ID 0x0280 - Atez module
    /// </summary>
    AtezModule = 0x280,
    /// <summary>
    /// CAN-ID 0x0301 - Remote control module
    /// </summary>
    RemoteControlModule = 0x301,
    /// <summary>
    /// CAN-ID 0x0302 - Remote control module 2
    /// </summary>
    RemoteControlModule2 = 0x302,
    /// <summary>
    /// CAN-ID 0x0303 - Remote control module 3
    /// </summary>
    RemoteControlModule3 = 0x303,
    /// <summary>
    /// CAN-ID 0x0400 - Room thermostat
    /// </summary>
    RoomThermostat = 0x400,
    /// <summary>
    /// CAN-ID 0x0480 - Manager
    /// </summary>
    Manager = 0x480,
    /// <summary>
    /// CAN-ID 0x0500 - Heating module
    /// </summary>
    HeatingModule = 0x500,
    /// <summary>
    /// CAN-ID 0x0580 - Bus coupler
    /// </summary>
    BusCoupler = 0x580,
    /// <summary>
    /// CAN-ID 0x0601 - Mixer module 1
    /// </summary>
    MixerModule = 0x601,
    /// <summary>
    /// CAN-ID 0x0602 - Mixer module 2
    /// </summary>
    MixerModule2 = 0x602,
    /// <summary>
    /// CAN-ID 0x0603 - Mixer module 3
    /// </summary>
    MixerModule3 = 0x603,
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
    DcfModule = 0x780
}
