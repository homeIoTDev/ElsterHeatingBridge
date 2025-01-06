using System;

namespace HeatingDaemon;

public class StandardCanFrame: CanFrame
{
    public StandardCanFrame(uint senderCanId, byte[] data): base(senderCanId, data)
    {
        if (senderCanId > 0x7FF) throw new ArgumentException("SenderCanId must be less or equal than 0x7FF");
    }

    /// <summary>
    /// Parsed ein CAN-Bus-Frame aus einem USB-TIN-String und gibt ein StandardCanFrame-Objekt zurück.
    /// </summary>
    public static StandardCanFrame ParseFromUsbTin(string inputFrame)
    {
        // Excpected format in inputFrame
        //  iiildd...
        // iii      = identifier in hex (000-7FF)
        // l        = Data length (0-8) 
        // dd...    = Byte value in hex (00-FF). Numbers of dd pairs must match
        //            the data length, otherwise an error occur
        uint senderCanId = Convert.ToUInt32(inputFrame.Substring(0, 3), 16);
        byte[] data = ParseDataFromInputFrame(inputFrame, 3);
        return new StandardCanFrame(senderCanId, data);
    }

    /// <summary>
    /// Erzeugt ein USB-TIN-String mit einem standard (11bit) CAN frame.
    /// </summary>
    /// <returns>Gibt einen USB-TIN-String zurueck</returns>
    public override string ToUsbTinString()
    {
        // tiiildd...  =  Transmit a standard (11bit) CAN frame.
        // iii      = identifier in hex (000-7FF)
        // l        = Data length (0-8) 
        // dd...    = Byte value in hex (00-FF). Numbers of dd pairs must match
        //            the data length, otherwise an error occur
        string retString = $"t{SenderCanId:X3}{Data.Length:X1}";
        retString += DataToString(false);
        return retString;
    }

    /// <summary>
    /// Gibt das Standard CAN-Frame als String zurück im Format
    /// iii [l] dd...
    /// wobei iii die Sender-ID in Hex, l die Datenlänge und dd... die Daten in Hex sind
    /// </summary>
    /// <returns>String, der das aktuelle Objekt darstellt</returns>
    public override string ToString()
    {
        string retString = $"{SenderCanId:X3} ";
        retString += DataToString();
        return retString;
    }
}
