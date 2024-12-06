using System;
using System.Reflection.Metadata.Ecma335;

namespace AC10Service;

/// <summary>
/// https://juerg5524.ch/data/Telegramm-Aufbau.txt:
/// Was ich bis jetzt noch nicht ganz verstanden habe, ist der Telegrammaufbau bzw. die ersten 16-Bit.
/// Wenn ich folgendes Telegramm habe:
/// A1 00FA 07A9 0000 bzw. 92 00FA 07A9 001D
/// dann ist mir noch nicht klar, was A1 00 (vom ersten Telegramm)bzw. 92 00 (vom zweiten Telegramm) bedeutet.
/// Es kommen auch Telegramme vor, welche mit A2 79 oder anderen Kombinationen beginnen. Ich habe bei 
/// meinen Mitschnitten bisher nicht eindeutig rausfinden können, was diese 16-Bit genau beschreiben. 
/// 
/// |1010 0001 0000 0000|10100001 00000000|10100001 00000000|00000000 00000000|
/// |A1 00|FA 07|A9 00|0000|
/// 
/// A1 00: bedeutet Anfrage (das ist das 2. Digit "1") an die CAN-ID 500. 
/// Die 500 setzt sich aus 8*(A0 & f0) + (00 & 0f) zusammen, d.h. das ertste Digit A0 mal 8 plus das 4. Digit 0. 
/// Demnach ist 61 02 eine Anfrage an die CAN-ID 302. 
/// Als Antwort auf A1 00 fa 07 49 (die beiden letzten Bytes kannst Du auch weglassen) erhältst Du:
/// D2 00 fa 07 49 xxxx. Wobei xxxx der gewünschte Wert ist und das erste Digit "D" gibt über den Sender von A100 Auskunft.
/// Das müsste sich dann um die CAN-ID des Senders 780 (8*d0) handeln. Das Zweite Digit von D2, also die "2", 
/// besagt, dass es sich um eine Antwort handelt, bzw. dass nach dem Elster-Index ein gültiger Wert steht.
///
/// 92 00: bedeutet Änderung eines Wertes. Die CAN-ID ist hier 8*90 + 0, also 480. 
/// Auch hier nach "fa" kommt der Elster-Index und danach der zu setzende Wert. Hier gibt es kein Antwort-Telegramm.
/// 
/// Die Telegramme, bei welchen 79 an 2. Stelle steht, sind "broadcast" Telegramme, die in regelmässigen
/// Zeitabständen abgesetzt werden.
/// 
/// An der 3. Stelle steht nicht notwendigerweise "fa" ("ERWEITERUNGSTELEGRAMM" siehe Elster-Tabelle). 
/// Wenn ein Elster-Index 2-stellig ist, also kleiner oder gleich ff ist, dann darf der
/// Index dort direkt eingesetzt werden. Das Resultat erhält man dann im 4. und 5. Byte. 
///
/// ====== Methode aus meiner C++-Sammlung:
///
/// void KCanFrame::InitElsterFrame(unsigned short sender_id,
///                                 unsigned short receiver_id,
///                                 unsigned short ElsterIdx)
/// {
///   unsigned char address = (receiver_id & 0x780) / 8;
///   Init();
///   Id = sender_id;
///   Len = 5;
///   Data[0] = (unsigned char)((address & 0xf0) + 1);
///   Data[1] = (receiver_id & 7);
///   Data[2] = 0xfa;
///   Data[3] = (unsigned char)(ElsterIdx >> 8);
///   Data[4] = (unsigned char)(ElsterIdx & 0xff);
/// }
/// </summary>
public class ElsterCANFrame
{
    //unsigned Counter;
    //int      TimeStampDay;
    //int      TimeStampMs;
    //int      Len;
    //unsigned Flags;

    public uint Id { get; set; }
    public byte[] Data { get; set; } = new byte[8];

    public ElsterCanId GetElsterCanId()
    {
        return (ElsterCanId)Id; 
    }

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

        if (Data[2] == 0xfa) //enhanced telegram with enhanced Elster-Index
        {
            if (Data.Length < 5) // than expected more data 
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

    internal int GetValue()
    {
        if (Data.Length < 5 || Data.Length > 7)
            return -1;
        
        if (Data[2] == 0xfa)  //enhanced telegram with enhanced Elster-Index
        {
            if (Data.Length != 7)
            {
                return -1;
            }
            return 256*Data[5] + Data[6];
        }
        return 256*Data[3] + Data[4];
    }

    internal string GetValueString()
    {

        short elsterIndex = GetElsterIdx();
        if (elsterIndex < 0)
            return "";
        int ind = KElsterTable.ElsterTabIndex[elsterIndex];
        if (ind < 0)
            return "";
        var elsterEntry = KElsterTable.ElsterTable[ind];
        KElsterTable.ElsterType elsterType = elsterEntry.Type;
        int elsterValue = GetValue();
        if (elsterValue ==-1)
            return "";
        return KElsterTable.GetValueString(elsterType, (short)elsterValue);    
    }
}
