using System;
using System.Data;
using System.Text;
using Microsoft.Extensions.Logging;
using static AC10Service.KElsterTable;

namespace AC10Service;
//soll AC10HeatingAdapter
internal class AC10HeatingAdapter
{
    private readonly ILogger<AC10HeatingAdapter>  _logger;
    private Action<string>?                       _sendLineCallback;
    private Action<string,string>?                _sendReadingCallback;

    public AC10HeatingAdapter(ILogger<AC10HeatingAdapter> logger)
    {
      _logger = logger;
    }
//Todo: Diese Methode sollte Schnell ausgefuehrt werden und
//Todo: sollte keine Excpeption werfen!
    public void ProcessCanFrame(CanFrame frame)
    {
        _logger.LogDebug($"Received frame {frame.ToString()}"); 
        ElsterCANFrame? elsterFrame = ElsterCANFrame.FromCanFrame(frame);
        if(elsterFrame != null) {
          if(elsterFrame.IsValidTelegram && elsterFrame.IsKnownElsterIndex)
          {
            _logger.LogDebug($"{elsterFrame.ToString()}");
          }
          else
          {
            _logger.LogWarning($"{elsterFrame.ToString()}");
          }
        }
    }


    public bool RequestElsterValue(ushort senderCanId,ushort receiverCanId, ushort elster_idx, ushort elster_value)
    {
      ElsterCANFrame  frame = new ElsterCANFrame(senderCanId,(ElsterModule)receiverCanId,ElsterTelegramType.Write,elster_idx,elster_value);

      /*
      if (Send() && RecvFrame.Len == 7)
      {
        int val = -1;
        
        val = RecvFrame.GetValue();
        if (val < 0)
          return false;
        
        Value = (unsigned short) val;
        
        return true;
      }
      */
      return false;
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
