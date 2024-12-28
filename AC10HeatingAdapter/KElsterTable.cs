using System;
using System.Text;

namespace AC10Service;

public partial class KElsterTable
{
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
}
