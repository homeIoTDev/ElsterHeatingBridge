using System;

namespace AC10Service;

public class UsbTinCanBusAdapterConfig
{
    /// <summary>
    /// Name des seriellen Ports, z.B. /dev/ttyACM0
    /// </summary>
    public string PortName { get; set; } = "/dev/ttyACM0";

    /// <summary>
    /// Baudrate des seriellen Ports, z.B. 115200
    /// </summary>  
    public int BaudRate { get; set; } = 115200;

    /// <summary>
    /// Anzahl der Datenbits, z.B. 8    
    /// </summary>
    public int DataBits { get; set; } = 8;

    /// <summary>   
    /// Stopbits, z.B. 1
    /// </summary>
    public int StopBits { get; set; } = 1;  

    /// <summary>
    /// Parity, z.B. None
    /// </summary>  
    public ParityEnum Parity { get; set; } = ParityEnum.None;

    /// <summary>
    /// Handshake, z.B. None
    /// </summary>
    public HandshakeEnum Handshake { get; set; } = HandshakeEnum.None;

    /// <summary>
    /// Enum for the Handshake options
    /// </summary>
    /// <remarks>
    /// This is a copy of the enum from the <see cref="System.IO.Ports.Handshake"/> enum
    /// </remarks>
    public enum HandshakeEnum
    {
        /// <summary>
        /// No handshaking protocol is used.
        /// </summary>
        None = 0,

        /// <summary>
        /// The Request to Send (RTS) signal is monitored. When RTS is asserted, data is transmitted.
        /// </summary>
        RequestToSend = 1,

        /// <summary>
        /// The Request to Send (RTS) and Clear to Send (CTS) signals are monitored. Data is transmitted only when both RTS and CTS are asserted.
        /// </summary>
        RequestToSendXOnXOff = 2,

        /// <summary>
        /// The XON/XOFF protocol is used to control data flow.
        /// </summary>
        XOnXOff = 3
    }

    /// <summary>
    /// Enum for the parity options
    /// </summary>
    /// <remarks>
    /// This is a copy of the enum from the <see cref="System.IO.Ports.Parity"/> enum
    /// </remarks>
    public enum ParityEnum
    {
        /// <summary>
        /// No parity
        /// </summary>
        None,
        /// <summary>
        /// Even parity
        /// </summary>
        Even,
        /// <summary>
        /// Odd parity
        /// </summary>
        Odd,
        /// <summary>
        /// Mark parity
        /// </summary>
        Mark,
        /// <summary>
        /// Space parity
        /// </summary>
        Space
    }
}