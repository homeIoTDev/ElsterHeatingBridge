using System;
using System.Text;

namespace AC10Service;

public partial class KElsterTable
{
   public enum ElsterType
   {
        // Hint: ElsterTypeStr dosnt not exist. Use enumeration instead
        et_default = 0,
        et_dec_val,       // Auflösung: xx.x / auch neg. Werte sind möglich
        et_cent_val,      // x.xx
        et_mil_val,       // x.xxx
        et_byte,
        et_bool,          // 0x0000 und 0x0001
        et_little_bool,   // 0x0000 und 0x0100
        et_double_val,
        et_triple_val,
        et_little_endian,
        et_betriebsart,
        et_zeit,
        et_datum,
        et_time_domain,
        et_dev_nr,
        et_err_nr,
        et_dev_id
        }

    [Obsolete("Use enumeration instead")]
    public static string[] ElsterTypeStr =
    {
        "et_default",
        "et_dec_val",
        "et_cent_val",
        "et_mil_val",
        "et_byte",
        "et_double_val",
        "et_triple_val",
        "et_little_endian",
        "et_zeit",
        "et_datum",
        "et_time_domain",
        "et_dev_nr",
        "et_err_nr",
        "et_dev_id"
    };

    /// <summary>
    /// Initializes the ElsterTabIndex array. This array is
    /// used to quickly find the index of a specific entry 
    /// in the ElsterTable array.
    /// </summary>
    /// <returns>The initialized ElsterTabIndex array</returns>
    private static short[] InitializeElsterTabIndex()
    {
        short[] elsterTabIndex = new short[0x10000]; 
        for (int i = 0; i < elsterTabIndex.Length; i++)
            elsterTabIndex[i] = -1;

        for (int i = 0; i < ElsterTable.Length; i++)
            if (elsterTabIndex[ElsterTable[i].Index] == -1)
                elsterTabIndex[ElsterTable[i].Index] = (short)i;
        return elsterTabIndex;
    }

    internal static string GetValueString(KElsterTable.ElsterType elsterType, short elsterValue)
    {
        StringBuilder retString = new StringBuilder();

        switch (elsterType)
        {
            case ElsterType.et_default:
                retString.Append(elsterValue.ToString());
                break;
            case ElsterType.et_dec_val:
                retString.AppendFormat("{0:F1}", ((double)elsterValue) / 10.0);
                break;
            case ElsterType.et_cent_val:
                retString.AppendFormat("{0:F2}", ((double)elsterValue) / 100.0);
                break;
            case ElsterType.et_mil_val:
                retString.AppendFormat("{0:F3}", ((double)elsterValue) / 1000.0);
                break;
            case ElsterType.et_byte:
                retString.Append(((sbyte)elsterValue).ToString());
                break;
            case ElsterType.et_bool:
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
            case ElsterType.et_little_bool:
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
            case ElsterType.et_double_val:
                retString.Append($"et_double_val:{elsterValue}");
                break;
            case ElsterType.et_triple_val:
                retString.Append($"et_triple_val:{elsterValue}");
                break;
            case ElsterType.et_little_endian:
                retString.Append(((elsterValue >> 8) + 256*(elsterValue & 0xff)).ToString());
                break;
            case ElsterType.et_zeit:
                retString.Append($"{(elsterValue & 0xff):D2}:{(elsterValue >> 8):D2}");
                break;
            case ElsterType.et_datum:
                retString.Append($"{(elsterValue >> 8):D2}.{(elsterValue & 0xff):D2}.");
                break;
            case ElsterType.et_time_domain:
                if ((elsterValue & 0x8080) != 0)
                    retString.Append("not used time domain");
                else
                    retString.AppendFormat("{0:D2}:{1:D2}-{2:D2}:{3:D2}",
                        (elsterValue >> 8) / 4, 15 * ((elsterValue >> 8) % 4),
                        (elsterValue & 0xff) / 4, 15 * (elsterValue % 4));
                break;
            case ElsterType.et_dev_nr:
                if (elsterValue >= 0x80)
                    retString.Append("--");
                else
                    retString.AppendFormat("{0}", elsterValue + 1);
                break;
            case ElsterType.et_err_nr:
                {
                int idx = Array.FindIndex(ErrorList, e => e.Index == elsterValue);
                if (idx >= 0)
                    retString.Append(ErrorList[idx].Name);
                else
                    retString.AppendFormat("ERR {0}", elsterValue);
                }
                break;
            case ElsterType.et_dev_id:
                retString.AppendFormat("{0}-{1:D2}", (elsterValue >> 8), elsterValue & 0xff);
                break;
            case ElsterType.et_betriebsart:
                if ((elsterValue & 0xff) == 0 && (elsterValue >> 8) < BetriebsartList.Length)
                    retString.Append(BetriebsartList[elsterValue >> 8].Name);
                else
                    retString.Append("?:et_betriebsart");
                break;
        }
      
        return retString.ToString();
    }
}
