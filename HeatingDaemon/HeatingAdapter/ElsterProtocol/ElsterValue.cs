using System;
using System.Text;
using System.Globalization;
using static HeatingDaemon.KElsterTable;

namespace HeatingDaemon;

public class ElsterValue
{
    private const ushort     ELSTER_NULL_VALUE = 0x8000;
    private byte[]          _valueByteArray;
    private ElsterValueType _elsterValueType;

    /// <summary>
    /// Die Elster-Wertetyp, die angibt, wie das Byte-Array interpretiert werden soll.
    /// </summary>
    public ElsterValueType ValueType { get => _elsterValueType; }

    /// <summary>
    /// Initialisiert eine neue Instanz von <see cref="ElsterValue"/> mit einem short-Wert und einem Elster-Wertetyp.
    /// </summary>
    /// <param name="value">Der zu speichernde Elster-Wert.</param>
    /// <param name="elsterValueType">Der Typ des Elster-Werts.</param>
    /// <remarks>
    /// Der <see cref="ElsterValue"/> wird intern als 2-Byte-Array gespeichert.
    /// </remarks>
    public ElsterValue(ushort value, ElsterValueType elsterValueType)
    {
        _valueByteArray     = BitConverter.GetBytes(value);
        _elsterValueType    = elsterValueType;
    }

    /// <summary>
    /// Fügt einen weiteren Elster-Wert zu einem bestehenden Multi-Wert hinzu. Wenn ein 
    /// Elster-Wert aus mehreren Telegrammen zusammengesetzt wird, wird mit dieser Methode entsprechend
    /// immer zwei Bytes an das internen Byte-Array angefügt.
    /// </summary>
    /// <param name="nextValue">Der nächste Elster-Wert, der hinzugefügt werden soll.</param>
    /// <returns>
    /// True, wenn der nächste Wert erfolgreich hinzugefügt wurde, andernfalls false. 
    /// Der nächste Wert wird nur hinzugefügt, wenn der aktuelle Werttyp 
    /// <see cref="ElsterValueType.et_double_val"/> oder <see cref="ElsterValueType.et_triple_val"/> ist,
    /// der nächste Wert 2 Bytes lang ist und die aktuelle Länge des Wertes im zulässigen Bereich liegt.
    /// </returns>
    public bool SetNextMultiValue(ElsterValue nextValue)
    {
        // Only accept double or triple values
        if (_elsterValueType != ElsterValueType.et_double_val && 
            _elsterValueType != ElsterValueType.et_triple_val)
            return false;
            
        // Next value must be 2 bytes
        if (nextValue._valueByteArray.Length != 2)
            return false;
            
        // First value must be 2 bytes for double_val, or 2/4 bytes for triple_val
        bool validLength = _elsterValueType == ElsterValueType.et_double_val ? 
            _valueByteArray.Length == 2 :
            (_valueByteArray.Length == 2 || _valueByteArray.Length == 4);
            
        if (!validLength)
            return false;
            
        _valueByteArray = _valueByteArray.Concat(nextValue._valueByteArray).ToArray();
        return true;
    }

    /// <summary>
    /// Konvertiert ein Byte-Array in einen spezifischen Datentyp basierend auf dem ElsterValueType.
    /// </summary>
    /// <param name="valueByteArray">Das zu konvertierende Byte-Array, welches den Wert enthält.</param>
    /// <param name="elsterValueType">Der Elster-Wertetyp, der bestimmt, wie das Byte-Array interpretiert werden soll.</param>
    /// <returns>
    /// Ein Objekt des entsprechenden Typs basierend auf dem ElsterValueType. Mögliche Rückgabetypen sind:
    /// - short: Für Standardwerte
    /// - double: Für Dezimal-, Cent- und Milliwerte
    /// - sbyte: Für Byte-Werte
    /// - bool?: Für boolesche Werte
    /// - (Hour, Minute): Für Zeitwerte
    /// - (Day, Month): Für Datumswerte
    /// - (StartTime, EndTime): Für Zeitbereichswerte
    /// - string: Für Fehlernummern, Geräte-IDs und Betriebsarten
    /// - null: Wenn der Wert nicht konvertiert werden kann oder ungültig ist
    /// </returns>
    private static object? ConvertByteArrayToType(byte[] valueByteArray, ElsterValueType elsterValueType)
    {
        // Der short (2 Byte) Wert wird mindestens genutzt im Array, ggf. aber mehr
        short shortValue = BitConverter.ToInt16(valueByteArray);
        switch (elsterValueType)
        {
            case ElsterValueType.et_default:
                return shortValue;
            case ElsterValueType.et_dec_val:
                return ((double)shortValue) / 10.0;
            case ElsterValueType.et_cent_val:
                return ((double)shortValue) / 100.0;
            case ElsterValueType.et_mil_val:
                return ((double)shortValue) / 1000.0;
            case ElsterValueType.et_byte:
                return (sbyte)shortValue;
            case ElsterValueType.et_bool:
                return (shortValue == 0x0001)? true: (shortValue==0)? false: null;
            case ElsterValueType.et_little_bool:
                return (shortValue == 0x0100)? true: (shortValue==0)? false: null;
            case ElsterValueType.et_double_val:
                // Es müssen zwei Telegramme mit je 2 Bytes im Array stehen und das erste darf nicht Elster-Null sein
                if(valueByteArray.Length != 4 || (ushort)shortValue == ELSTER_NULL_VALUE) return null; 
                return (double)shortValue + (double)(BitConverter.ToUInt16(valueByteArray, 2)) / 1000.0;  
            case ElsterValueType.et_triple_val:
                // Es müssen drei Telegramme mit je 2 Bytes im Array stehen und das erste darf nicht Elster-Null sein
                if(valueByteArray.Length != 6 || (ushort)shortValue == ELSTER_NULL_VALUE) return null;
                return shortValue + BitConverter.ToUInt16(valueByteArray, 2) / 1000.0 + BitConverter.ToUInt16(valueByteArray, 4) / 1000000.0;
            case ElsterValueType.et_little_endian:
                return ((shortValue >> 8) + 256*(shortValue & 0xff));  //signed value
            case ElsterValueType.et_zeit:
                return (Hour: (ushort)shortValue >> 8, Minute: (ushort)shortValue  & 0xff);
            case ElsterValueType.et_datum:
                return (Day: (ushort)shortValue  >> 8, Month: (ushort)shortValue  & 0xff);
            case ElsterValueType.et_time_domain:
                if ((ushort)shortValue == ELSTER_NULL_VALUE) return null; // not used time domain
                int startMinutes = (((ushort)shortValue >> 8) * 15);
                int endMinutes = ((ushort)shortValue & 0xff) * 15;
                return (
                    StartTime: TimeSpan.FromMinutes(startMinutes), 
                    EndTime: TimeSpan.FromMinutes(endMinutes)
                );
            case ElsterValueType.et_dev_nr:
                if ((ushort)shortValue == 0x80) return null;    // Laut KElsterTable.cpp:138 >= 0x80 -> nicht verwendet ?
                return (ushort)(shortValue + 1);                // CAN-ID?
            case ElsterValueType.et_err_nr:                     // Gibt immer gleich den String zurueck
                {
                int idx = Array.FindIndex(ErrorList, e => e.Index == (ushort)shortValue);
                if (idx >= 0)
                    return ErrorList[idx].Name;
                else
                    return $"ERR {(ushort)shortValue}";
                }
            case ElsterValueType.et_dev_id: // Was ist einen dev-id im format x-x ?
                return $"{((ushort)shortValue >> 8)}-{(ushort)shortValue & 0xff}";;
            case ElsterValueType.et_betriebsart:
                if (((ushort)shortValue & 0xff) == 0 && ((ushort)shortValue >> 8) < BetriebsartList.Length)
                    return BetriebsartList[(ushort)shortValue >> 8].Name;
                else
                    return $"?:et_betriebsart{(ushort)shortValue}";
            default:
                return null;
        }
    }
   
   /// <summary>
   /// Gibt zurueck, ob der Wert Elster-Null (0x8000) ist. Ist der Wer
   /// </summary>
   /// <returns></returns>
   public bool IsElsterNullValue() => (ushort)(GetShortValue() ?? 0) == ELSTER_NULL_VALUE;

    /// <summary>
    /// Gibt den short-Wert des ElsterValues zurück (et_default).
    /// </summary>
    /// <returns>Der short-Wert des ElsterValues oder null wenn der Typ nicht passt.</returns>
    public short? GetShortValue() => (short?)ConvertByteArrayToType(_valueByteArray, ElsterValueType.et_default);
    /// <summary>
    /// Gibt den double-Wert mit einer Dezimalstelle des ElsterValues zurück (et_dec_val).
    /// </summary>
    /// <returns>Der double-Wert des ElsterValues mit einer Dezimalstelle oder null wenn der Typ nicht passt.</returns>
    public double? GetDecimalValue() => (double?)ConvertByteArrayToType(_valueByteArray, ElsterValueType.et_dec_val);
    /// <summary>
    /// Gibt den double-Wert mit zwei Dezimalstellen des ElsterValues zurück (et_cent_val).
    /// </summary>
    /// <returns>Der double-Wert des ElsterValues mit zwei Dezimalstellen oder null wenn der Typ nicht passt.</returns>
    public double? GetCentValue() => (double?)ConvertByteArrayToType(_valueByteArray, ElsterValueType.et_cent_val);
    /// <summary>
    /// Gibt den double-Wert mit drei Dezimalstellen des ElsterValues zurück (et_mil_val).
    /// </summary>
    /// <returns>Der double-Wert des ElsterValues mit drei Dezimalstellen oder null wenn der Typ nicht passt.</returns>
    public double? GetMilValue() => (double?)ConvertByteArrayToType(_valueByteArray, ElsterValueType.et_mil_val);
    /// <summary>
    /// Gibt den sbyte-Wert des ElsterValues zurück (et_byte).
    /// </summary>
    /// <returns>Der sbyte-Wert des ElsterValues oder null wenn der Typ nicht passt.</returns>
    public sbyte? GetByteValue() => (sbyte?)ConvertByteArrayToType(_valueByteArray, ElsterValueType.et_byte);
    /// <summary>
    /// Gibt den bool-Wert des ElsterValues zurück(et_bool).
    /// </summary>
    /// <returns>Der bool-Wert des ElsterValues oder null wenn der Typ nicht passt.</returns>
    public bool? GetBooleanValue() => (bool?)ConvertByteArrayToType(_valueByteArray, ElsterValueType.et_bool);
    /// <summary>
    /// Gibt den bool-Wert des ElsterValues aus einem Little-Endian-Form zurück (et_little_bool).
    /// </summary>
    /// <returns>Der bool-Wert des ElsterValues in Little-Endian-Form oder null wenn der Typ passt.</returns>
    public bool? GetLittleBooleanValue() => (bool?)ConvertByteArrayToType(_valueByteArray, ElsterValueType.et_little_bool);
    /// <summary>
    /// Gibt den double-Wert des ElsterValues zurück (et_double_val, das aus zwei Telegrammen besteht).
    /// </summary>
    /// <returns>Der double-Wert des ElsterValues oder null wenn der Typ nicht passt.</returns>
    public double? GetDoubleValue() => (double?)ConvertByteArrayToType(_valueByteArray, ElsterValueType.et_double_val);
    /// <summary>
    /// Gibt den double-Wert des ElsterValues zurück (et_triple_val, das aus drei Telegrammen besteht).
    /// </summary>
    /// <returns>Der double-Wert des ElsterValues oder null wenn der Typ nicht passt.</returns>
    public double? GetTripleValue() => (double?)ConvertByteArrayToType(_valueByteArray, ElsterValueType.et_triple_val);
    /// <summary>
    /// Gibt den int-Wert des ElsterValues aus einem Little-Endian-Form zurück.(et_little_endian).
    /// </summary>
    /// <returns>Der int-Wert des ElsterValues oder null wenn der Typ nicht passt.</returns>
    public int? GetLittleEndianValue() => (int?)ConvertByteArrayToType(_valueByteArray, ElsterValueType.et_little_endian);
    /// <summary>
    /// Gibt den string-Wert des ElsterValues aus der Liste-Betriebsarten wie z.B 'Notbetrieb' zurück (et_betriebsart).
    /// </summary>
    /// <returns>Der string-Wert des ElsterValues aus der Liste-Betriebsarten oder null wenn der Typ nicht passt.</returns>
    public string? GetBetriebsartValue() => (string?)ConvertByteArrayToType(_valueByteArray, ElsterValueType.et_betriebsart);
    /// <summary>
    /// Gibt den TimeSpan-Wert des ElsterValues in der Form "HH:mm" zurück (et_zeit).
    /// </summary>
    /// <returns>Der TimeSpan-Wert des ElsterValues in der Form "HH:mm" oder null wenn der Typ nicht passt.</returns>
    public TimeSpan? GetTimeValue() => (TimeSpan?)ConvertByteArrayToType(_valueByteArray, ElsterValueType.et_zeit);
    /// <summary>
    /// Gibt den (int Day, int Month)-Wert des ElsterValues in der Form "DD-MM" zurück (et_datum).
    /// </summary>
    /// <returns>Der (int Day, int Month)-Wert des ElsterValues in der Form "DD-MM" oder null wenn der Typ nicht passt.</returns>
    public (int Day, int Month)? GetDateValue() => ((int Day, int Month)?)ConvertByteArrayToType(_valueByteArray, ElsterValueType.et_datum);
    /// <summary>
    /// Gibt den (TimeSpan StartTime, TimeSpan EndTime)-Wert des ElsterValues in der Form "HH:mm-HH:mm" zurück (et_time_domain).
    /// </summary>
    /// <returns>Der (TimeSpan StartTime, TimeSpan EndTime)-Wert des ElsterValues in der Form "HH:mm-HH:mm" oder null wenn der Typ passt.</returns>
    public (TimeSpan StartTime, TimeSpan EndTime)? GetTimeDomainValue() => ((TimeSpan StartTime, TimeSpan EndTime)?)ConvertByteArrayToType(_valueByteArray, ElsterValueType.et_time_domain);
    /// <summary>
    /// Gibt die DeviceNr als ushort-Wert des ElsterValues zurück (et_dev_nr).
    /// </summary>
    /// <returns>Die DeviceNr als ushort-Wert des ElsterValues oder null wenn der Typ nicht passt.</returns>
    public ushort? GetDeviceNrValue() => (ushort?)ConvertByteArrayToType(_valueByteArray, ElsterValueType.et_dev_nr);
    /// <summary>
    /// Gibt die Device-Id als string im Format x-x zurück. (et_dev_id).
    /// </summary>
    /// <returns>Die Device-Id als string im Format x-x oder null wenn der Typ nicht passt.</returns>
    public string? GetDeviceIdValue() => (string?)ConvertByteArrayToType(_valueByteArray, ElsterValueType.et_dev_id);
    /// <summary>
    /// Gibt den string-Wert aus der ErrorList des ElsterValues zurück (et_err_nr).
    /// </summary>
    /// <returns>Der string-Wert aus der ErrorList des ElsterValues oder null wenn der Typ nicht passt.</returns>
    public string? GetErrorNrValue() => (string?)ConvertByteArrayToType(_valueByteArray, ElsterValueType.et_err_nr);
    /// <summary>
    /// Gibt den Wert des ElsterValues in einem Object zurück.
    /// </summary>
    /// <returns>Der Wert des ElsterValues in einem Object oder null wenn der Typ nicht bekannt ist.</returns>
    public object? GetValue()
    {
        return ConvertByteArrayToType(_valueByteArray, _elsterValueType);
    }

    /// <summary>
    /// Gibt eine durch Kommas getrennte Zeichenkette aller möglichen Interpretationen des Byte-Array-Wertes
    /// basierend auf verschiedenen ElsterValueType-Konvertierungen zurück.
    /// </summary>
    /// <returns>
    /// Eine Zeichenkette, die alle gültigen Wertinterpretationen im Format "Typ: Wert" enthält,
    /// getrennt durch Kommas. Enthält nur Konvertierungen, die zu Nicht-Null-Werten führen.
    /// </returns>
    public string GetAllPossibleInterpretableValues()
    {
        StringBuilder retString = new StringBuilder();

        foreach (ElsterValueType type in Enum.GetValues(typeof(ElsterValueType)))
        {
            object? value = ConvertByteArrayToType(_valueByteArray, type);
            if (value != null)
            {
                if (retString.Length > 0)
                {
                    retString.Append(", ");
                }
                retString.Append($"{type}: {value}");
            }
        }
        return retString.ToString();
    }

    /// <summary>
    /// Gibt den Wert des ElsterValues als Hex-String zurück.
    /// </summary>
    /// <returns>Der Hex-String des ElsterValues.</returns>
    public string ToHexString()
    {
        StringBuilder retString = new StringBuilder();
        short elsterValue = BitConverter.ToInt16(_valueByteArray);
        retString.AppendFormat("{0:X4}", elsterValue);
        return retString.ToString();

    }

    /// <summary>
    /// Returns the value of the ElsterValue as a string.
    /// If the value is unknown or invalid, it also returns the raw data.
    /// The values are formatted in invariant culture, so they can be easily 
    /// further processed, e.g. under FHEM, i.e. 23.4 instead of 23,4
    /// </summary>
    public override string ToString()
    {
        StringBuilder retString = new StringBuilder();
        short elsterValue = BitConverter.ToInt16(_valueByteArray);

        switch (_elsterValueType)
        {
            case ElsterValueType.et_default:
                retString.AppendFormat("{0} (0x{0:X4})", elsterValue);
                break;
            case ElsterValueType.et_dec_val:
                retString.AppendFormat(CultureInfo.InvariantCulture,"{0:F1}", ((double)elsterValue) / 10.0);
                break;
            case ElsterValueType.et_cent_val:
                retString.AppendFormat(CultureInfo.InvariantCulture,"{0:F2}", ((double)elsterValue) / 100.0);
                break;
            case ElsterValueType.et_mil_val:
                retString.AppendFormat(CultureInfo.InvariantCulture,"{0:F3}", ((double)elsterValue) / 1000.0);
                break;
            case ElsterValueType.et_byte:
                retString.Append(((sbyte)elsterValue).ToString());
                break;
            case ElsterValueType.et_bool:
                if (elsterValue == 0x0001)
                {
                    retString.Append("on");
                }
                else
                {
                    if (elsterValue == 0) {
                        retString.Append("off");
                    }
                    else {
                        retString.Append($"0x{(ushort)elsterValue:X4}?:et_bool");
                    }
                }
                break;
            case ElsterValueType.et_little_bool:
                if (elsterValue == 0x0100)
                {
                    retString.Append("on");
                }
                else
                {
                    if (elsterValue == 0) {
                        retString.Append("off");
                    }
                    else {
                        retString.Append($"0x{(ushort)elsterValue:X4} ?:et_little_bool");
                    }
                }
                break;
            case ElsterValueType.et_double_val:
                double? retDoubleValue = GetDoubleValue();
                if(retDoubleValue == null)
                {
                    retString.Append( $"et_double_val:{elsterValue}");
                }
                else
                {
                    retString.AppendFormat(CultureInfo.InvariantCulture,"{0:F3}", retDoubleValue);
                }
                break;
            case ElsterValueType.et_triple_val:
                double? retTripleValue = GetTripleValue();
                if(retTripleValue == null)
                {
                    retString.Append( $"et_triple_val:{elsterValue}");
                }
                else
                {
                    retString.AppendFormat(CultureInfo.InvariantCulture,"{0:F3}", retTripleValue);
                }
                break;
            case ElsterValueType.et_little_endian:
                retString.Append(((elsterValue >> 8) + 256*(elsterValue & 0xff)).ToString());
                break;
            case ElsterValueType.et_zeit:
                retString.Append($"{(elsterValue & 0xff):D2}:{(elsterValue >> 8):D2}");
                break;
            case ElsterValueType.et_datum:
                retString.Append($"{(elsterValue >> 8):D2}.{(elsterValue & 0xff):D2}.");
                break;
            case ElsterValueType.et_time_domain:
                if ((elsterValue & 0x8080) != 0)
                    retString.Append("not used time domain");
                else
                    retString.AppendFormat("{0:D2}:{1:D2}-{2:D2}:{3:D2}",
                        (elsterValue >> 8) / 4, 15 * ((elsterValue >> 8) % 4),
                        (elsterValue & 0xff) / 4, 15 * (elsterValue % 4));
                break;
            case ElsterValueType.et_dev_nr:
                if (elsterValue >= 0x80)
                    retString.Append("--");
                else
                    retString.AppendFormat("{0}", elsterValue + 1);
                break;
            case ElsterValueType.et_err_nr:
                {
                int idx = Array.FindIndex(ErrorList, e => e.Index == (ushort)elsterValue);
                if (idx >= 0)
                    retString.Append(ErrorList[idx].Name);
                else
                    retString.AppendFormat("ERR {0}", (ushort)elsterValue);
                }
                break;
            case ElsterValueType.et_dev_id:
                retString.AppendFormat("{0}-{1:D2}", ((ushort)elsterValue)>> 8, (ushort)elsterValue & 0xff);
                break;
            case ElsterValueType.et_betriebsart:
                byte betriebsart = (byte)(elsterValue >> 8);
                if(betriebsart==0x80) 
                    retString.Append("et_betriebsart:unavailable");
                else if ((elsterValue & 0xff) == 0 && betriebsart < BetriebsartList.Length)
                    retString.Append(BetriebsartList[elsterValue >> 8].Name);
                else
                    retString.Append($"0x{(ushort)elsterValue:X4} ?:et_betriebsart");
                break;
        }
      
        return retString.ToString();
    }
}
