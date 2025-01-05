using System;

namespace AC10Service;

/// <summary>
/// Bekannte CAN-IDs in den Elster-Systemen. Ja nach System können auch mehrere Module gleichen Typs 
/// mit unterschiedlichen CAN-IDs verwendet werden (z.B. RemoteControl und RemoteControl2).
/// </summary>
/// <remarks>
/// https://knx-user-forum.de/forum/öffentlicher-bereich/knx-eib-forum/code-schnipsel/26505-anbindung-tecalor-ttw13?p=634595#post634595
/// </remarks>
public enum ElsterModule
{
    /// <summary>
    /// CAN-ID 0x000 - Direct.
    /// </summary>
    /// <remarks>
    /// Queries with short Elster telegrams should not be performed on this module (currently only long telegrams are supported anyway).
    /// The central error list can be queried here.
    /// </remarks>
    Direct = 0x000,
    /// <summary>
    /// CAN-ID 0x0100 - FES Comfort module 
    /// </summary>
    /// <remarks>
    /// This module is directly located at the WPM3. If the placement is also the same as the living space (e.g. not in an unheated basement),
    /// then possibly no extra FEK is required.
    /// </remarks>
    FES_COMFORT = 0x100,
    /// <summary>
    /// CAN-ID 0x0179 - FES Comfort module broadcast
    /// </summary>
    FES_COMFORT_Broadcast = 0x0179,
    /// <summary>
    /// CAN-ID 0x0180 - Boiler module
    /// </summary>
    Boiler = 0x0180,
    /// <summary>
    /// CAN-ID 0x01F9 - Boiler broadcast to all boiler modules
    /// </summary>
    Boiler_Broadcast = 0x1F9,  
    /// <summary>
    /// CAN-ID 0x0280 - Atez module
    /// </summary>
    /// <remarks>
    /// Ich vermute hier das zusätzliche Heizmodule für den Warmwasser-Boilder, was bei Solarzellen sinnvoll sein kann
    /// </remarks>
    AtezModule = 0x280,
    /// <summary>
    /// CAN-ID 0x2F9 - Atez module broadcast
    /// </summary>
    AtezModule_Broadcast =0x2F9,
    /// <summary>
    /// CAN-ID 0x0301 - Remote control module (FEK)
    /// </summary>
    RemoteControl = 0x301,
    /// <summary>
    /// CAN-ID 0x0379 - Remote control module broadcast
    /// </summary>
    RemoteControl_Broadcast = 0x379,
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
    /// CAN-ID 0x0479 - Room thermostat broadcast
    /// </summary>
    RoomThermostat_Broadcast = 0x479,
    /// <summary>
    /// CAN-ID 0x0480 - Manager
    /// </summary>
    Manager = 0x480,
    /// <summary>
    /// CAN-ID 0x04F9 - Manager broadcast
    /// </summary>
    Manager_Broadcast=0x4F9,
    /// <summary>
    /// CAN-ID 0x0500 - Electric Heater Control Module
    /// </summary>
    HeatingModule = 0x500,
    /// <summary>
    /// CAN-ID 0x0579 - Electric Heater Control Module broadcast
    /// </summary>
    HeatingModule_Broadcast = 0x579,
    /// <summary>
    /// CAN-ID 0x0580 - Bus coupler
    /// </summary>
    BusCoupler = 0x580,
    /// <summary>
    /// CAN-ID 0x05F9 - Bus coupler broadcast
    /// </summary>
    BusCoupler_Broadcast=0x5F9,
    /// <summary>
    /// CAN-ID 0x0601 - Mixer module 1
    /// </summary>
    Mixer = 0x601,
    /// <summary>
    /// CAN-ID 0x0679 - Mixer module broadcast
    /// </summary>
    Mixer_Broadcast=0x679,
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
    /// CAN-ID 0x06F9 - PC (ComfortSoft). Don't use as sender id with WPM3.
    /// </summary>
    ComfortSoft_Broadcast=0x6F9,
    /// <summary>
    /// CAN-ID 0x0700 - External device
    /// </summary>
    ExternalDevice = 0x700,
    /// <summary>
    /// CAN-ID 0x0779 - External device broadcast
    /// </summary>
    ExternalDevice_Broadcast=0x779,
    /// <summary>
    /// CAN-ID 0x0780 - DCF module
    /// </summary>
    Dcf = 0x780,
    /// <summary>
    /// CAN-ID 0x07F9 - DCF module broadcast
    /// </summary>
    Dcf_Broadcast=0x7F9
}
