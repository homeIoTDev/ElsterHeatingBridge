using System;
using System.Globalization;

namespace HeatingDaemon.ElsterProtocol;

public class ElsterUserInputParser
{
    /// <summary>
    /// Parses a string representation of an Elster value into a nullable ushort.
    /// </summary>
    /// <param name="elsterValueStr">The string to parse, which can be a hexadecimal number with optional "0x" prefix.</param>
    /// <returns>
    /// The corresponding ushort value if parsing succeeds; null if the input is null, empty, whitespace,
    /// or cannot be parsed as a valid hexadecimal number.
    /// </returns>
    public static ushort? ParseElsterValue(string? elsterValueStr)
    {
        if(string.IsNullOrWhiteSpace(elsterValueStr) )
        {
            return null;
        }
        else if (ushort.TryParse(elsterValueStr.Replace("0x", ""), NumberStyles.HexNumber, null, out var elsterValueHex))
        {
            return elsterValueHex;
        } 
        else // keine Hexzahl, dann geht es nicht
        {
            return null;
        }
    }

    /// <summary>
    /// Parses a string representation of an Elster index into an ushort? value. 
    /// The input string can be either a hexadecimal number (with optional "0x" prefix) or an Elster index name.
    /// </summary>
    /// <param name="elsterIndexStr">The string to parse.</param>
    /// <returns>
    /// The corresponding ushort? value if parsing succeeds; null if the input is null, 
    /// empty, whitespace, or cannot be parsed as either a valid hexadecimal number or an Elster index name.
    /// </returns>
    public static ushort? ParseElsterIndex(string? elsterIndexStr)
    {
        if(string.IsNullOrWhiteSpace(elsterIndexStr) )
        {
            return null;
        }
        else if (ushort.TryParse(elsterIndexStr.Replace("0x", ""), NumberStyles.HexNumber, null, out var elsterIndexHex))
        {
            return elsterIndexHex;
        } 
        else if (KElsterTable.ElsterTabIndexName.TryGetValue(elsterIndexStr, out var elsterIndexName))
        {
            return elsterIndexName;
        }
        else // Weder Elster-Index-Name noch eine Hexzahl, dann geht es nicht
        {
            return null;
        }
    }

    /// <summary>
    /// Parses a string representation of a receiver Elster module identifier into an ElsterModule enum value.
    /// </summary>
    /// <param name="receiverCanIDStr">The string to parse, which can be either a 
    /// hexadecimal CAN ID (with optional "0x" prefix) or an ElsterModule enum name.</param>
    /// <returns>
    /// The corresponding ElsterModule enum value if parsing succeeds; null if the input is null, empty, whitespace,
    /// or cannot be parsed as either a valid hexadecimal number or an ElsterModule enum value.
    /// </returns>
    public static ElsterModule? ParseReceiverElsterModule(string? receiverCanIDStr)
    {
        if (string.IsNullOrWhiteSpace(receiverCanIDStr))
        {
            return null;
        }

        ushort canId;
        if (ushort.TryParse(receiverCanIDStr.Replace("0x", ""), NumberStyles.HexNumber, null, out canId))
        {
            return (ElsterModule)canId;
        } 
        else if (Enum.TryParse<ElsterModule>(receiverCanIDStr, out var elsterModule))
        {
            return elsterModule;
        }
        else // Weder ElsterModule-Name noch eine Hexzahl, dann geht es nicht
        {
            return null;
        }
    }

    /// <summary>
    /// Parses a string representation of an Elster module identifier into an ElsterModule enum value.
    /// </summary>
    /// <param name="receiverCanIDStr">The string to parse, which can be either a 
    /// hexadecimal CAN ID (with optional "0x" prefix) or an ElsterModule enum name.</param>
    /// <returns>
    /// The corresponding ElsterModule enum value if parsing succeeds; null if the input is null, empty, whitespace,
    /// or cannot be parsed as either a valid hexadecimal number or an ElsterModule enum value.
    /// </returns>
    /// If parsing fails, returns 0xFFF which will be replaced by the default CAN-ID when sending.
    /// </remarks>
    public static ElsterModule ParseSenderElsterModule(string? senderCanIDStr) 
    {
        ElsterModule senderCanID;
        ushort canId;
        if (string.IsNullOrWhiteSpace(senderCanIDStr))
        {
            senderCanID = (ElsterModule)0xFFF;  // 0xFFF ist ungültig und wird beim Senden durch die Standard-CAN-ID ersetzt 
        }
        else if (ushort.TryParse(senderCanIDStr.Replace("0x", ""), NumberStyles.HexNumber, null, out canId))
        {
            senderCanID = (ElsterModule)canId;
        } 
        else if (Enum.TryParse<ElsterModule>(senderCanIDStr, out var elsterModule))
        {
            senderCanID = elsterModule;
        }
        else // Weder ElsterModule-Name noch eine Hexzahl, dann Standard-CAN-ID
        {
            senderCanID = (ElsterModule)0xFFF;  // 0xFFF ist ungültig und wird beim Senden durch die Standard-CAN-ID ersetzt 
        }
        return senderCanID;
    }
}
