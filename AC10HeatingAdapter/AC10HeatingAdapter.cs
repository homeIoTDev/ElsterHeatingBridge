using System;
using System.Data;
using System.Text;
using Microsoft.Extensions.Logging;

namespace AC10Service;
//soll AC10HeatingAdapter
internal class AC10HeatingAdapter
{
    private readonly ILogger<AC10HeatingAdapter> _logger;

    private Action<string>?         _sendLineCallback;
    private Action<string,string>?  _sendReadingCallback;

    public AC10HeatingAdapter(ILogger<AC10HeatingAdapter> logger)
    {
        _logger = logger;
    }

    public void ProcessCanFrame(CanFrame frame)
    {
        _logger.LogDebug($"Received frame {frame.ToString()}"); 
        ElsterCANFrame? elsterFrame = ElsterCANFrame.FromCanFrame(frame);
        if(elsterFrame != null) {
             LogElsterCanFrame(elsterFrame);
        }
    }

    bool LogElsterCanFrame(ElsterCANFrame frame)
    {
        StringBuilder str = new StringBuilder();

        if (frame.Data.Length != 7)  
            return false;
        string fromDevice = frame.GetElsterCanId().ToString();
        short elsterIndex = frame.GetElsterIdx();
        if (elsterIndex < 0)
            return false;
        int ind = KElsterTable.ElsterTabIndex[elsterIndex];
        if (ind < 0)
        {
            _logger.LogError($"Elster CAN frame from {fromDevice} with elster index {elsterIndex:X4} not found, with possible data: {frame.GetValue()} frame: {frame}");
            return false;
        }
        var elsterEntry = KElsterTable.ElsterTable[ind];
        _logger.LogDebug($"{fromDevice} {elsterEntry.Name} = {KElsterTable.GetValueString(elsterEntry.Type, (short)frame.GetValue())}");
        
        return true;

/*


  int elster_idx = GetElsterIdx();
      if (elster_idx >= 0)
      {
        const ElsterIndex * elst_ind = GetElsterIndex(elster_idx);
        int Value = GetValue();
        int d = Data[0] & 0xf;
        if (Value >= 0 && d != 1) // 1 is request flag
        {
          if (elst_ind)
            SetValueType(OutStr + strlen(OutStr), elst_ind->Type, (unsigned short) Value);
          else
            sprintf(OutStr + strlen(OutStr), "%d", (unsigned) Value);
          strcat(OutStr, " ");
        }
        strcat(OutStr, elst_ind ? elst_ind->Name : "?");
      }
    } else {
      for (int i = 0; i < 8; i++)
        if (i < Len)
          sprintf(OutStr + strlen(OutStr), "%c",
                  ' ' <= Data[i] && Data[i] < 127 ? Data[i] : '.');
    }
  }
  */


    }

    private bool SendLine(String line)
    {
        if (_sendLineCallback != null )
        {
            _sendLineCallback(line);
            return true;
        }

        return false;
    }

    public void Start(Action<string> sendLineCallback, Action<string,string> sendReadingCallback)
    {
        _logger.LogInformation("Starting AC10HeatingAdapter...");
        _sendLineCallback       = sendLineCallback;
        _sendReadingCallback    = sendReadingCallback;    
    }
}
