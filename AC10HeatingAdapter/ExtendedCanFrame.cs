namespace AC10Service;

public class ExtendedCanFrame : CanFrame
{
    public ExtendedCanFrame(uint senderCanId, byte[] data) : base(senderCanId, data)
    {
        if (senderCanId > 0x1FFFFFFF) throw new ArgumentException("SenderCanId must be less or equal than 0x1FFFFFFF");
    }

    public static ExtendedCanFrame ParseFromUsbTin(string inputFrame)
    {
        // Excpected format in inputFrame
        //  iiiiiiiildd...
        // iiiiiiii = Identifier in hex (00000000-1FFFFFFF)
        // l        = Data length (0-8) 
        // dd...    = Byte value in hex (00-FF). Numbers of dd pairs must match
        //            the data length, otherwise an error occur
        uint senderCanId = Convert.ToUInt32(inputFrame.Substring(0, 8), 16);
        byte[] data = ParseDataFromInputFrame(inputFrame, 8);
        return new ExtendedCanFrame(senderCanId, data);
    }

    public override string ToString()
    {
        string retString = $"{SenderCanId:X8} ";
        retString += DataToString();
        return retString;
    }
}
