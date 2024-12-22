using System;
using System.Reflection.Metadata.Ecma335;
using System.Text;
using static AC10Service.KElsterTable;

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
    //Todo: Fehlermeldung über Elster sollten ausgewerten wreden: RemoteControl ->Write ComfortSoft FEHLERMELDUNG = 20805
    //unsigned Counter;
    //int      TimeStampDay;
    //int      TimeStampMs;
    //int      Len;
    //unsigned Flags;

    private ElsterCANFrame(uint senderCanId, byte[] data)
    {
        this.SenderCanId    = senderCanId;
        Data                = data;
        Initialize();
    }

    public ElsterCANFrame(uint senderCanId, ElsterModule receiverElsterModule, ElsterTelegramType telegramType, ushort elsterIndex, ushort value, bool enhancedTelegram = false)
    {
        if(!enhancedTelegram)
        {
            Data = new byte[4];
        }
        else
        {
            Data = new byte[7];
        }
        this.SenderCanId    = senderCanId;
        SetReceiverCanId((uint)receiverElsterModule);
        SetTelegramType(telegramType);
        SetElsterIndex(elsterIndex);
        SetValue(value);
        Initialize();
    }
    private void Initialize()
    {
        this.ReceiverCanId  = GetReceiverCanId();
        ValidateAndGenerateToString();
    }

    public byte[]   Data                { get; private set; } 
    public uint     SenderCanId         { get; private set; }
    public uint     ReceiverCanId       { get; private set; }
    public bool     IsValidTelegram     { get; private set; }
    public bool     IsKnownElsterIndex  { get; private set; }
    /// <summary>
    /// The Elster Index. If < 0, the Elster Index is invalid.
    /// </summary>
    public short   ElsterIndex         { get; private set; }
    private string toStringString ="";

    public ElsterModule SenderElsterModule {get { return (ElsterModule)SenderCanId; }}

    public ElsterModule ReceiverElsterModule {
        get { return (ElsterModule)ReceiverCanId; }
        set { 
                SetReceiverCanId((uint)value);
                Initialize(); // Update all properties 
            }
    }
    public ElsterTelegramType TelegramType { 
        get { return GetTelegramType(); }
        set { 
                SetTelegramType(value);
                Initialize(); // Update all properties 
            } 
     }


    public static ElsterCANFrame? FromCanFrame(CanFrame canFrame)
    {
        ElsterCANFrame elsterCANFrame   = new ElsterCANFrame(canFrame.SenderCanId, canFrame.Data);
        return elsterCANFrame;
    }
    /// <summary>
    /// Gets the Receiver CAN ID from the Data array.
    /// </summary>
    /// <returns>The Receiver CAN ID, or 0xFFFF if Data array is too short</returns>
    internal uint GetReceiverCanId()
    {
        if (Data.Length < 2)
            return 0xFFFF;  // no receiver can id = error
        uint retValue = (uint)(((Data[0] & 0xF0) << 3) + (Data[1] & 0x7f));

        return retValue;
    }

    /// <summary>
    /// Sets the Receiver CAN ID in the Data array. The array must be at least 2 bytes long.
    /// </summary>
    /// <param name="receiverCanId">The Receiver CAN ID to set.</param>
    /// <exception cref="Exception">Thrown if Data.Length is less than 2.</exception>
    private void SetReceiverCanId(uint receiverCanId)
    {
        if (Data.Length < 2) throw new Exception("Data.Length < 2");
        byte data0 = (byte)((receiverCanId >> 3) & 0xf0);
        byte data1 = (byte)(receiverCanId & 0x7f);
        Data[0] = (byte)(((int)Data[0] & 0x0f) | (int)data0);
        Data[1] = (byte)(((int)Data[1] & 0x80) | (int)data1);
    }

    /// <summary>
    /// Gets the Telegram Type from the Data array.
    /// </summary>
    /// <returns>Elster Telegram Type or ElsterTelegramType.Unknown, 
    /// if Data array is too short</returns>
    private ElsterTelegramType GetTelegramType()
    {
        if (Data.Length > 0)
        {
            int telegramTypInfo =(Data[0]&0x0F);
            return (ElsterTelegramType)telegramTypInfo;
        }
        return ElsterTelegramType.Unknown;
    }

    /// <summary>
    /// Sets the Telegram Type in the Data array. The array must be at least 1 byte long.
    /// </summary>
    /// <param name="telegramType">The Telegram Type to set.</param>
    /// <exception cref="Exception">Thrown if Data.Length is less than 1.</exception>
    private void SetTelegramType(ElsterTelegramType telegramType)
    {
        if (Data.Length < 1) throw new Exception("Data.Length < 1");
        if (telegramType == ElsterTelegramType.Unknown) throw new Exception("telegramType == ElsterTelegramType.Unknown");
        Data[0] = (byte)(((int)Data[0] & 0xF0) | (int)telegramType);
    }

    /// <summary>
    /// Gibt zurück, ob diese Elster CAN-Frame ein Broadcast-Telegramm ist für eine
    /// Modul-Gruppe. Eine Modul-Gruppe besteht z.B aus allen Wärmepumpen-Managern, so z.B. aus WPM1 und WPM2.
    /// In diesem Fall ist das zweite Datenbyte immer 0x79. Das Setzten für eine Broadcast-Telegramm
    /// für eine Modul-Gruppe kann direkt über <see cref="ReceiverElsterModule"/> oder über 
    /// <see cref="SetReceiverModuleBroadcast"/> erfolgen.
    /// </summary>
    /// <returns>True, wenn es ein Broadcast für eine Module-Gruppe ist</returns>
    public bool IsReceiverModuleBroadcast()
    {
        return Data[1] == 0x79; 
    }

    /// <summary>
    /// Setzt das zweite Datenbyte auf 0x79, so dass die Elster CAN-Frame ein Broadcast-Telegramm ist
    /// für eine Modul-Gruppe. Eine Modul-Gruppe besteht z.B aus allen Wärmepumpen-Managern,
    /// so z.B. aus WPM1 und WPM2. Die Module-Gruppe muss vorher mit <see cref="ReceiverElsterModule"/>
    /// gesetzt worden sein. Man kann auch direkt über <see cref="ReceiverElsterModule"/> eine Broadcast
    /// für eine Module-Gruppe setzten. Mit <see cref="IsReceiverModuleBroadcast"/> kann abgefragt
    /// werden, ob es sich um einen Broadcast fuer eine Module-Gruppe handelt.
    /// </summary>
    public void SetReceiverModuleBroadcast()
    { 
        if (Data.Length < 2) throw new Exception("Data.Length < 2");
        Data[1] = 0x79;
        Initialize(); // Update all properties 
    }
    
  


    /// <summary>
    /// Retrieves the Elster Index from the Data array.
    /// </summary>
    /// <returns>
    /// The Elster Index as a short. Returns -1 if the Data array length 
    /// is invalid or if additional data is expected but not present.
    /// </returns>
    /// <remarks>
    /// The function checks if the telegram is enhanced (indicated by 0xfa at Data[2]).
    /// If it is enhanced, it expects the Elster Index to be a two-byte value (Data[3] and Data[4]).
    /// Otherwise, it uses the value at Data[2] as the Elster Index.
    /// </remarks>
    private short GetElsterIndex()
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

    private void SetElsterIndex(ushort idx)
    {
        if( idx == 0xfa ) 
        {
           throw new Exception("Can't set explicit an anhanced telegram");  // we can't set an anhanced telegram
        } 
        //If Data is initialised with 5 bytes, we have an short telegram,
        //so we can set an elster index below 256. 
        if(Data.Length == 5)
        {
            if (idx < 0x100)
            {
                Data[2] = (byte) idx;
            } 
        }
        //If Data is initialised with 7 bytes, we have an enhanced telegram,
        //so we can set any elster index, even below 256.
        if(Data.Length == 7)
        {
            Data[2] = 0xfa;
            Data[3] = (byte) (idx >> 8);
            Data[4] = (byte) idx;
        }
        else
            throw new Exception("Wrong length of data array, expected 5 or 7");  // false length of data array
    }

//Todo: Alle Property sollen get und set Methoden haben, wie SetReceiverCanId
    private int GetValue()
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

    bool SetValue(ushort value)
    {
        if (Data.Length != 5 && Data.Length != 7)
            return false;

        if (Data[2] == 0xfa) //enhanced telegram with enhanced Elster-Index
        {
            Data[5] = (byte)(value >> 8);
            Data[6] = (byte)(value & 0xff);
        } else {
            Data[3] = (byte)(value >> 8);
            Data[4] = (byte)(value & 0xff);
        }
        return true;
    }

    internal string GetValueString()
    {
        short elsterIndex = GetElsterIndex();
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

    /// <summary>
    /// Validiert die Elster CAN-Frame und erzeugt einen String für die Ausgabe
    /// über <see cref="ToString"/>.
    /// </summary>
    private void ValidateAndGenerateToString()
    {
        toStringString                  = "";
        IsKnownElsterIndex              = false;
        StringBuilder str               = new StringBuilder();
        string fromDeviceModule         = Enum.IsDefined(typeof(ElsterModule), (int)SenderCanId) ? SenderElsterModule.ToString() : $"{SenderCanId:X3}";
        string fromDeviceCanIdInvalid   = SenderCanId > 0x7ff ? "(invalid)" : "";
        string toDeviceCanIdInvalid     = ReceiverCanId > 0x7ff ? "(invalid)" : "";

        if (Data.Length != 7) { 
            toStringString = $"Elster CAN frame from {fromDeviceModule}{fromDeviceCanIdInvalid} with incorrect data length, expected 7. [{Data.Length}] {DataToString()}";
            IsValidTelegram = false;
            return;
        }

        string broadcastString  = IsReceiverModuleBroadcast() ? "(broadcast)" : "";
        string toDeviceModule   = Enum.IsDefined(typeof(ElsterModule), (int)ReceiverCanId) ? ReceiverElsterModule.ToString() : $"{ReceiverCanId:X3}{broadcastString}";
             
        ElsterIndex = GetElsterIndex();
        if (ElsterIndex < 0)
        {
            toStringString  = $"Elster CAN frame from {fromDeviceModule}{fromDeviceCanIdInvalid} ->{TelegramType} on {toDeviceModule}{toDeviceCanIdInvalid} without elster index.  [{Data.Length}] {DataToString()}";
            IsValidTelegram = false;
            return;
        }

        //Letzte Prüfung, ob es überhaupt gültige CAN-Ids für Sender oder Empfänger gesetzt sind
        IsValidTelegram = (SenderCanId> 0x7ff) || (ReceiverCanId > 0x7ff) || this.TelegramType == ElsterTelegramType.Unknown? false : true;
        int ind = KElsterTable.ElsterTabIndex[ElsterIndex];
        if (ind < 0)
        {
            toStringString  = $"Elster CAN frame from {fromDeviceModule}{fromDeviceCanIdInvalid} ->{TelegramType} on {toDeviceModule}{toDeviceCanIdInvalid} with unknown elster index {ElsterIndex:X4}, with possible data: '{GetValue()}' [{Data.Length}] {DataToString()}";
            return;
        }

        IsKnownElsterIndex  = true;
        var elsterEntry     = KElsterTable.ElsterTable[ind];
        string elsterValue  = "= "+ KElsterTable.GetValueString(elsterEntry.Type, (short)GetValue());
        //If this is a request, then the value is always 0 and also unimportant, as it is being requested
        if(TelegramType == ElsterTelegramType.Read) {
            elsterValue = "";
        }
        toStringString = $"{fromDeviceModule}{fromDeviceCanIdInvalid} ->{TelegramType} on {toDeviceModule}{toDeviceCanIdInvalid} {elsterEntry.Name} {elsterValue}";
    }

    /// <summary>
    /// Returns a string with the following 
    /// format: {fromDeviceModule}{fromDeviceCanIdInvalid} ->{TelegramType} on {toDeviceModule}{toDeviceCanIdInvalid} {elsterEntry.Name} {elsterValue}
    /// </summary>
    /// <returns>string that represents the current object</returns>
    public override string ToString()
    {
        return toStringString;
    }

    private string DataToString()
    {
        string retString = $"[{Data.Length:X1}] ";
        for (int i = 0; i < Data.Length; i++)
            retString += $"{Data[i]:X2}";
        return retString;
    }
}
