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
      ElsterCANFrame  frame = new ElsterCANFrame(
                                      senderCanId,
                                      (ElsterModule)receiverCanId,
                                      ElsterTelegramType.Write,
                                      elster_idx,
                                      elster_value);

      StandardCanFrame sendCanFrame = frame.ToCanFrame();
      string usbTinString = sendCanFrame.ToUsbTinString();
      if( SendCanFrame(sendCanFrame) == true)
      {
          //Warten bis eine Antwort passend zum versendeten Frame kommt oder ein Timeout auftritt
          //TODO: Timeout implementieren
          return true;
      }
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
