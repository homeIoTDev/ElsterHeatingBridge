using System;

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

    public static readonly short[] ElsterTabIndex = InitializeElsterTabIndex();

    /// <summary>
    /// Initializes the ElsterTabIndex array. This array is
    /// used to quickly find the index of a specific entry 
    /// in the ElsterTable array.
    /// </summary>
    /// <returns>The initialized ElsterTabIndex array</returns>
    private static short[] InitializeElsterTabIndex()
    {
        short[] elsterTabIndex = new short[0x10000]; 
        for (int i = 0; i < ElsterTabIndex.Length; i++)
            ElsterTabIndex[i] = -1;

        for (int i = 0; i < ElsterTable.Length; i++)
            if (ElsterTabIndex[ElsterTable[i].Index] == -1)
                ElsterTabIndex[ElsterTable[i].Index] = (short)i;
        return ElsterTabIndex;
    }

}
