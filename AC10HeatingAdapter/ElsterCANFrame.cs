using System;

namespace AC10Service;

public class ElsterCANFrame
{
    //unsigned Counter;
    //int      TimeStampDay;
    //int      TimeStampMs;
    //int      Len;
    //unsigned Flags;

    public uint Id { get; set; }
    public byte[] Data { get; set; } = new byte[8];

    public static ElsterCANFrame? FromCanFrame(CanFrame canFrame)
    {
        ElsterCANFrame elsterCANFrame   = new ElsterCANFrame();
        elsterCANFrame.Id               = canFrame.SenderCanId;
        elsterCANFrame.Data             = canFrame.Data;
        return elsterCANFrame;
    }

    internal short GetElsterIdx()
    {
        if (Data.Length > 7 || Data.Length < 3)
            return -1;

        if (Data[2] == 0xfa)
        {
            if (Data.Length < 5)
            {
                return -1;
            }

            return (short)(256*Data[3] + Data[4]);
        } 
        else
        {
            return Data[2];
        }
    }
}
