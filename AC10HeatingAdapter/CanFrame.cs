namespace AC10Service;

public abstract class CanFrame
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

    /// <summary>
    /// Erzeugt ein USB-TIN-String zum Senden aus dem aktuellen CAN frame (Standard oder Erweitert).
    /// </summary>
    /// <returns>Gibt einen USB-TIN-String zurück</returns>
    public abstract string ToUsbTinString();

    /// <summary>
    /// Gibt die Daten als Hex-String zurück. Wenn <paramref name="withLength"/> true ist, 
    /// wird die Länge der Daten mit angegeben im Format [L] ddddd zurueckgegeben. Ohne 
    /// <paramref name="withLength"/> wird nur die Daten als Hex zurückgegeben
    /// </summary>
    /// <param name="withLength"></param>
    /// <returns>Daten als Hex-String</returns>
    protected string DataToString(bool withLength = true)
    {
        string retString = (withLength)?$"[{Data.Length:X1}] ":"";
        for (int i = 0; i < Data.Length; i++)
            retString += $"{Data[i]:X2}";
        return retString;
    }

}
