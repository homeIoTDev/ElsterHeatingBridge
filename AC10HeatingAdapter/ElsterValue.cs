using System;
using System.Text;
using static AC10Service.KElsterTable;

namespace AC10Service;

public class ElsterValue
{
    public const ushort ELSTER_NULL_VALUE = 0x8000;
    private byte[] _valueByteArray;
    private ElsterValueType _elsterValueType;
    public ElsterValue(ushort value, ElsterValueType elsterValueType)
    {
        _valueByteArray     = BitConverter.GetBytes(value);
        _elsterValueType    = elsterValueType;
    }

    public ElsterValue(byte[] valueByteArray, ElsterValueType elsterValueType)
    {
        if( valueByteArray.Length < 2) throw new ArgumentException("valueByteArray has to be minimum of 2 bytes");
        _valueByteArray     = valueByteArray;
        _elsterValueType    = elsterValueType;
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
                return (Hour: (ushort)shortValue  >> 8, Minute: (ushort)shortValue  & 0xff);
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

    public object? GetValue()
    {
        return ConvertByteArrayToType(_valueByteArray, _elsterValueType);
    }

    public string GetValueString()
    {
        StringBuilder retString = new StringBuilder();
        short elsterValue = BitConverter.ToInt16(_valueByteArray);

        switch (_elsterValueType)
        {
            case ElsterValueType.et_default:
                retString.Append(elsterValue.ToString());
                break;
            case ElsterValueType.et_dec_val:
                retString.AppendFormat("{0:F1}", ((double)elsterValue) / 10.0);
                break;
            case ElsterValueType.et_cent_val:
                retString.AppendFormat("{0:F2}", ((double)elsterValue) / 100.0);
                break;
            case ElsterValueType.et_mil_val:
                retString.AppendFormat("{0:F3}", ((double)elsterValue) / 1000.0);
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
                        retString.Append("?:et_bool");
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
                        retString.Append("?:et_little_bool");
                    }
                }
                break;
            case ElsterValueType.et_double_val:
                retString.Append($"et_double_val:{elsterValue}");
                break;
            case ElsterValueType.et_triple_val:
                retString.Append($"et_triple_val:{elsterValue}");
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
                int idx = Array.FindIndex(ErrorList, e => e.Index == elsterValue);
                if (idx >= 0)
                    retString.Append(ErrorList[idx].Name);
                else
                    retString.AppendFormat("ERR {0}", elsterValue);
                }
                break;
            case ElsterValueType.et_dev_id:
                retString.AppendFormat("{0}-{1:D2}", (elsterValue >> 8), elsterValue & 0xff);
                break;
            case ElsterValueType.et_betriebsart:
                if ((elsterValue & 0xff) == 0 && (elsterValue >> 8) < BetriebsartList.Length)
                    retString.Append(BetriebsartList[elsterValue >> 8].Name);
                else
                    retString.Append("?:et_betriebsart");
                break;
        }
      
        return retString.ToString();
    }
}
