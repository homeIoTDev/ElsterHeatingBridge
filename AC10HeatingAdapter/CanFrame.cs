namespace AC10Service;

public class CanFrame
{
    public uint SenderCanId { get; set; }
    public byte[] Data { get; set; }

    public CanFrame(uint senderCanId, byte[] data)
    {
        SenderCanId = senderCanId;
        Data        = data;
    }

    protected static byte[] ParseDataFromInputFrame(string inputFrame, int dataStartIndex)
    {
        int dataLength = Convert.ToInt32(inputFrame.Substring(dataStartIndex, 1), 16);
        if (dataLength > 8) throw new Exception("Data length cannot be greater than 8");
        byte[] data = new byte[dataLength];
        for (int i = 0; i < dataLength; i++)
        {
            data[i] = Convert.ToByte(inputFrame.Substring(dataStartIndex + 1 + i * 2, 2), 16);
        }
        return data;
    }

    internal string DataToString()
    {
        string retString = $"[{Data.Length:X1}] ";
        for (int i = 0; i < Data.Length; i++)
            retString += $"{Data[i]:X2}";
        return retString;
    }
}
