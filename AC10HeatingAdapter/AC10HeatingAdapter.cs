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
    private Func<CanFrame, bool>?                 _sendCanFrameCallback;
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
      ElsterCANFrame  elsterFrame = new ElsterCANFrame(
                                      senderCanId,
                                      (ElsterModule)receiverCanId,
                                      ElsterTelegramType.Write,
                                      elster_idx,
                                      elster_value);
     
      
      StandardCanFrame sendCanFrame = elsterFrame.ToCanFrame();
      
      string usbTinString = sendCanFrame.ToUsbTinString();
      if( SendCanFrame(sendCanFrame) == true)
      {
          //Warten bis eine Antwort passend zum versendeten Frame kommt oder ein Timeout auftritt
          //TODO: Timeout implementieren
          return true;
      }
      /*
bool KCanElster::GetDoubleValue(unsigned short first_val,
                                unsigned scan_can_id,
                                unsigned short elster_idx,
                                unsigned char elster_type,
                                double & result)
{
  if (first_val == 0x8000)
    return false;

  if (elster_type != et_double_val &&
      elster_type != et_triple_val)
    return false;

  unsigned short sec_val;
  unsigned short third_val;
  NUtils::SleepMs(100);
  if (!GetValue(scan_can_id, elster_idx - 1, sec_val))
    return false;

  result = (double)(first_val) + (double)(sec_val) / 1000.0;

  if (elster_type == et_triple_val)
  {
    NUtils::SleepMs(100);
    if (!GetValue(scan_can_id, elster_idx - 2, third_val))
      return false;

    result += (double)(third_val) / 1000000.0;
  }
  return true;
}




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
   
    /// <summary>
    /// Sendet ein CAN-Frame an den CAN-Bus über ein Callback.
    /// </summary>
    /// <param name="frame">CAN-Frame, der gesendet werden soll</param>
    private bool SendCanFrame(CanFrame frame)
    {
        if (_sendCanFrameCallback != null )
        {
            return _sendCanFrameCallback(frame);
        }
        return false;
    }

    public void Start(Func<CanFrame, bool> sendCanFrameCallback, Action<string, string> sendReadingCallback)
    {
      _logger.LogInformation("Starting AC10HeatingAdapter...");
      _sendCanFrameCallback = sendCanFrameCallback;
      _sendReadingCallback  = sendReadingCallback;
    }
}
