using System;

namespace AC10Service;

public class StandardCanFrame: CanFrame
{
    public StandardCanFrame(uint senderCanId, byte[] data): base(senderCanId, data)
    {
        if (senderCanId > 0x7FF) throw new ArgumentException("SenderCanId must be less or equal than 0x7FF");
    }

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

    public override string ToString()
    {
        string retString = $"{SenderCanId:X3} ";
        retString += DataToString();
        return retString;
    }
}
