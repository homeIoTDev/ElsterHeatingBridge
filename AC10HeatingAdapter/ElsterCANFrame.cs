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
    //unsigned Counter;
    //int      TimeStampDay;
    //int      TimeStampMs;
    //int      Len;
    //unsigned Flags;

    private string _toStringString  = "";   // Cache für ToString(), wird in ValidateAndGenerateToString() gesetzt
    private ushort _elsterIndex     = 0xfa; // Das Member ElsterIndex, das mit  0xfa fehlerhaft ist, wird in ValidateAndGenerateToString() gesetzt.
   

    /// <summary>
    /// Initialisiert eine neue Instanz der <see cref="ElsterCANFrame"/> Klasse.
    /// Nimmt die Sender-CAN-ID und das Datenarray aus einem CAN-Bus-Frame und validiert es.
    /// Nach der Validierung werden alle Eigenschaften gesetzt.
    /// </summary>
    private ElsterCANFrame(uint senderCanId, byte[] data)
    {
        this.SenderCanId    = senderCanId;
        Data                = data;
        Initialize();
    }

    /// <summary>
    /// Initialisiert eine neue Instanz der <see cref="ElsterCANFrame"/> Klasse.
    /// Dieser Konstruktor wird zum Senden von Telegrammen auf dem CAN-Bus verwendet.
    /// Er nimmt die Sender-CAN-ID und generiert das Datenarray aus den anderen Parametern.
    /// Alle Parameter werden validiert und nach der Validierung werden alle Eigenschaften gesetzt.
    /// </summary>
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
    
    /// <summary>
    /// Gibt das Datenarray des Telegramms zurueck.
    /// </summary>
    public byte[]   Data                { get; private set; }
    /// <summary>
    /// Gibt zurueck, von welcher CAN-ID das Telegramm ausgeht.
    /// </summary>
    public uint     SenderCanId         { get; private set; }
    /// <summary>
    /// Gibt zurueck, an welche CAN-ID das Telegramm gerichtet ist.
    /// </summary>
    public uint     ReceiverCanId       { get; private set; }
    /// <summary>
    /// Gibt zurueck, ob das Telegramm gültig ist. Dem entsprechend 
    /// sind weitere Eigenschaft gültig oder nicht
    /// </summary>
    public bool     IsValidTelegram     { get; private set; }
    /// <summary>
    /// Gibt zurück, ob der Elster Index bekannt ist und 
    /// </summary>
    public bool     IsKnownElsterIndex  { get; private set; }
    /// <summary>
    /// The Elster Index of the telegram. Is only valid, if IsValidTelegram is true.
    /// </summary>
    public ushort   ElsterIndex         { 
        get { return _elsterIndex; }
        set { 
                SetElsterIndex(value);
                Initialize(); // Update all properties 
            } 
    }

    /// <summary>
    /// The Elster Module, which is the sender of the telegram.
    /// This property is readonly and will be set over <see cref="SenderCanId"/>.
    /// It is possible that the ElsterModule enum has an unknown value, so only the numerical value can be used.
    /// </summary>
    public ElsterModule SenderElsterModule {get { return (ElsterModule)SenderCanId; }}

    /// <summary>
    /// Setzt oder gibt die Empfänger-CAN-ID aus dem Datenarray zurueck.
    /// </summary>
    public ElsterModule ReceiverElsterModule {
        get { return (ElsterModule)ReceiverCanId; }
        set { 
                SetReceiverCanId((uint)value);
                Initialize(); // Update all properties 
            }
    }

    /// <summary>
    /// Setzt oder gibt den Telegrammtyp aus dem Datenarray zurueck.
    /// </summary>
    public ElsterTelegramType TelegramType { 
        get { return GetTelegramType(); }
        set { 
                SetTelegramType(value);
                Initialize(); // Update all properties 
            } 
     }

    /// <summary>
    /// Erstellt eine neue Instanz der <see cref="ElsterCANFrame"/> Klasse aus einem CAN-Bus-Frame.
    /// </summary>
    /// <param name="canFrame">Das CAN-Bus-Frame</param>
    /// <returns>Neue Instanz der <see cref="ElsterCANFrame"/> Klasse oder null, wenn das CAN-Frame ungueltig ist</returns>
    public static ElsterCANFrame? FromCanFrame(CanFrame canFrame)
    {
        ElsterCANFrame elsterCANFrame   = new ElsterCANFrame(canFrame.SenderCanId, canFrame.Data);
        return elsterCANFrame;
    }

    /// <summary>
    /// Erstellt eine neue Instanz der <see cref="CanFrame"/> Klasse aus der <see cref="ElsterCANFrame"/> Klasse.
    /// </summary>
    /// <returns>Neue Instanz der <see cref="CanFrame"/> Klasse</returns>
    public StandardCanFrame ToCanFrame()
    {
        return new StandardCanFrame(SenderCanId, Data);
    }

    /// <summary>
    /// Initialisiert die Eigenschaften der <see cref="ElsterCANFrame"/> Klasse.
    /// </summary>
    private void Initialize()
    {
        this.ReceiverCanId  = GetReceiverCanId();
        ValidateAndGenerateToString();
    }

    /// <summary>
    /// Gibt die Empfänger-CAN-ID aus dem Datenarray zurück.
    /// </summary>
    /// <returns>Die Empfänger-CAN-ID oder 0xFFFF, wenn das Datenarray zu kurz ist</returns>
    internal uint GetReceiverCanId()
    {
        if (Data.Length < 2)
            return 0xFFFF;  // no receiver can id = error
        uint retValue = (uint)(((Data[0] & 0xF0) << 3) + (Data[1] & 0x7f));

        return retValue;
    }

    /// <summary>
    /// Setzt die Empfänger-CAN-ID im Datenarray. Das Array muss mindestens 2 Bytes lang sein.
    /// </summary>
    /// <param name="receiverCanId">Die zu setzende Empfänger-CAN-ID.</param>
    /// <exception cref="Exception">Ausgelöst, wenn Data.Length kleiner als 2 ist.</exception>
    private void SetReceiverCanId(uint receiverCanId)
    {
        if (Data.Length < 2) throw new Exception("Data.Length < 2");
        byte data0 = (byte)((receiverCanId >> 3) & 0xf0);
        byte data1 = (byte)(receiverCanId & 0x7f);
        Data[0] = (byte)(((int)Data[0] & 0x0f) | (int)data0);
        Data[1] = (byte)(((int)Data[1] & 0x80) | (int)data1);
    }

    /// <summary>
    /// Gibt den Telegrammtyp aus dem Datenarray zurück.
    /// </summary>
    /// <returns>Elster Telegrammtyp oder ElsterTelegramType.Unknown, 
    /// wenn das Datenarray zu kurz ist</returns>
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
    /// Setzt den Telegrammtyp im Datenarray. Das Array muss mindestens 1 Byte lang sein.
    /// </summary>
    /// <param name="telegramType">Der zu setzende Telegrammtyp.</param>
    /// <exception cref="Exception">Ausgelöst, wenn Data.Length kleiner als 1 ist.</exception>
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
    /// Ruft den Elster-Index aus dem Datenarray ab.
    /// </summary>
    /// <returns>
    /// Den Elster-Index als short. Gibt -1 zurück, wenn die Länge des Datenarrays 
    /// ungültig ist oder wenn zusätzliche Daten erwartet werden, aber nicht vorhanden sind.
    /// </returns>
    /// <remarks>
    /// Die Funktion überprüft, ob das Telegramm ein Erweitungstelegram ist (angezeigt durch 0xfa bei Data[2]).
    /// Wenn es erweitert ist, wird erwartet, dass der Elster-Index ein zwei-Byte-Wert ist (Data[3] und Data[4]).
    /// Andernfalls wird der Wert bei Data[2] als Elster-Index verwendet.
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

    /// <summary>
    /// Setzt den Elster-Index im Datenarray. Das Array muss mindestens 3 Bytes lang sein.
    /// </summary>
    /// <param name="idx">Der zu setzende Elster-Index.</param>
    /// <exception cref="Exception">Ausgelöst, wenn Data.Length kleiner als 3 ist.</exception>
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

    /// <summary>
    /// Ruft den Wert aus dem Datenarray ab. Der Typ vom Wert ist abhängig vom ElsterIndex
    /// </summary>
    /// <returns>Zwei Byte als short, die den Wert enthalten, 
    /// oder -1, wenn die Länge des Datenarrays ungültig ist</returns>
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

    /// <summary>
    /// Setzt den Wert im Datenarray. Das Array muss mindestens 5 Bytes lang sein.
    /// </summary>
    /// <param name="value">Der zu setzende Wert.</param>
    /// <returns>True, wenn der Wert erfolgreich gesetzt wurde, sonst false.</returns>
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

    /// <summary>
    /// Gibt den Elster-Wert, entsprechend der interpretiertem ElsterIndex, als String zurück.
    /// </summary>
    /// <returns>Der Wert als String</returns>
    public string GetValueString()
    {
        short elsterIndex = GetElsterIndex();
        if (elsterIndex < 0)
            return "";
        int ind = KElsterTable.ElsterTabIndex[elsterIndex];
        if (ind < 0)
            return "";
        var elsterEntry = KElsterTable.ElsterTable[ind];
        ElsterValueType elsterType = elsterEntry.Type;
        int elsterValue = GetValue();
        if (elsterValue ==-1)
            return "";
        ElsterValue value = new ElsterValue((ushort)elsterValue, elsterType);
        return value.GetValueString();    
    }

    /// <summary>
    /// Validiert die Elster CAN-Frame und erzeugt einen String für die Ausgabe
    /// über <see cref="ToString"/>.
    /// </summary>
    private void ValidateAndGenerateToString()
    {
        _toStringString                  = "";
        IsKnownElsterIndex              = false;
        StringBuilder str               = new StringBuilder();
        string fromDeviceModule         = Enum.IsDefined(typeof(ElsterModule), (int)SenderCanId) ? SenderElsterModule.ToString() : $"{SenderCanId:X3}";
        string fromDeviceCanIdInvalid   = SenderCanId > 0x7ff ? "(invalid)" : "";
        string toDeviceCanIdInvalid     = ReceiverCanId > 0x7ff ? "(invalid)" : "";

        if (Data.Length != 7) { 
            _toStringString = $"Elster CAN frame from {fromDeviceModule}{fromDeviceCanIdInvalid} with incorrect data length, expected 7. [{Data.Length}] {DataArrayToString()}";
            IsValidTelegram = false;
            return;
        }

        string broadcastString  = IsReceiverModuleBroadcast() ? "(broadcast)" : "";
        string toDeviceModule   = Enum.IsDefined(typeof(ElsterModule), (int)ReceiverCanId) ? ReceiverElsterModule.ToString() : $"{ReceiverCanId:X3}{broadcastString}";
             
        short elsterIndex = GetElsterIndex();
        if (elsterIndex < 0)
        {
            _toStringString  = $"Elster CAN frame from {fromDeviceModule}{fromDeviceCanIdInvalid} ->{TelegramType} on {toDeviceModule}{toDeviceCanIdInvalid} without elster index.  [{Data.Length}] {DataArrayToString()}";
            IsValidTelegram = false;
            return;
        }
        //ElsterIndex übernehmen und ist gültig
        _elsterIndex = (ushort)elsterIndex;

        //Letzte Prüfung, ob es überhaupt gültige CAN-Ids für Sender oder Empfänger gesetzt sind
        IsValidTelegram = (SenderCanId> 0x7ff) || (ReceiverCanId > 0x7ff) || this.TelegramType == ElsterTelegramType.Unknown? false : true;
        int ind = KElsterTable.ElsterTabIndex[ElsterIndex];
        if (ind < 0)
        {
            _toStringString  = $"Elster CAN frame from {fromDeviceModule}{fromDeviceCanIdInvalid} ->{TelegramType} on {toDeviceModule}{toDeviceCanIdInvalid} with unknown elster index {ElsterIndex:X4}, with possible data: '{GetValue()}' [{Data.Length}] {DataArrayToString()}";
            return;
        }

        IsKnownElsterIndex      = true;
        var elsterEntry         = KElsterTable.ElsterTable[ind];
        string elsterValueString= GetValueString(); 
        //If this is a request, then the value is always 0 and also unimportant, as it is being requested
        if(TelegramType == ElsterTelegramType.Read) {
            elsterValueString = "";
        }
        _toStringString = $"{fromDeviceModule}{fromDeviceCanIdInvalid} ->{TelegramType} on {toDeviceModule}{toDeviceCanIdInvalid} {elsterEntry.Name} {elsterValueString}";
    }

    /// <summary>
    /// Gibt einen String mit folgendem Format zurück:
    /// {fromDeviceModule}{fromDeviceCanIdInvalid} ->{TelegramType} auf {toDeviceModule}{toDeviceCanIdInvalid} {elsterEntry.Name} {elsterValue}
    /// </summary>
    /// <returns>String, der das aktuelle Objekt darstellt</returns>
    public override string ToString()
    {
        return _toStringString;
    }

    /// <summary>
    /// Gibt einen String mit folgendem Format zurück:
    /// {fromDeviceModule}{fromDeviceCanIdInvalid} ->{TelegramType} auf {toDeviceModule}{toDeviceCanIdInvalid} {elsterEntry.Name} {elsterValue}
    /// </summary>
    /// <remarks>
    /// Der String wird von der Methode <see cref="ValidateAndGenerateToString"/> generiert.
    /// </remarks>
    private string DataArrayToString()
    {
        string retString = $"[{Data.Length:X1}] ";
        for (int i = 0; i < Data.Length; i++)
            retString += $"{Data[i]:X2}";
        return retString;
    }
}
