using System;
using System.Data;
using System.Text;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Collections.Concurrent;
using System.Threading;
using static AC10Service.KElsterTable;

namespace AC10Service;

public class AC10HeatingAdapter : IDisposable, IHeatingService
{
    private readonly ILogger<AC10HeatingAdapter>    _logger;
    private ICanBusService                          _canBusService;
    private bool                                    _storeResponseFramesInQueue = false;
    private readonly BlockingCollection<ElsterCANFrame>_responseFramesQueue = new(new ConcurrentQueue<ElsterCANFrame>());
    private object                                  _sendLock = new();
    private AC10HeatingAdapterConfig                _heatingAdapterConfig;

    public AC10HeatingAdapter(IOptions<AC10HeatingAdapterConfig> heatingAdapterConfig,
                              ICanBusService canBusService,
                              ILogger<AC10HeatingAdapter> logger)
    {
      _heatingAdapterConfig = heatingAdapterConfig.Value;
      _canBusService = canBusService;
      _logger = logger;
    }

//Todo: Diese Methode sollte Schnell ausgefuehrt werden und
//Todo: sollte keine Excpeption werfen!
    public void ProcessCanFrame(CanFrame frame)
    {
        _logger.LogDebug($"{frame.CreatedAt.ToString("dd.MM.yy HH:mm:ss.fff")} -> {frame.ToString()}"); 
        ElsterCANFrame? elsterFrame = ElsterCANFrame.FromCanFrame(frame);
        
        if(_storeResponseFramesInQueue)
        {
          if( elsterFrame != null && (
              elsterFrame.TelegramType == ElsterTelegramType.Respond ||
              elsterFrame.TelegramType == ElsterTelegramType.RespondSystem ) )
              {
                _responseFramesQueue.Add(elsterFrame);  //Enqueue
              }
        }

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

 private bool RequestMultiValueTelegram(ElsterCANFrame sendFrame, ref ElsterValue elsterValue)
    {
        if (elsterValue.ValueType != ElsterValueType.et_double_val && 
            elsterValue.ValueType != ElsterValueType.et_triple_val)
            return false;
        //Copy erstellen, wir wolle nicht das orginal verändern
        ElsterCANFrame sendElsterFrame2 = new ElsterCANFrame(sendFrame);

        for(int i=0; i < (elsterValue.ValueType == ElsterValueType.et_double_val?1:2); i++)
        {
          sendElsterFrame2.ElsterIndex  = (ushort)(sendElsterFrame2.ElsterIndex - 1);
          ElsterCANFrame? responseFrame = null;
          //Thread.Sleep(100);  // 100ms warten, aber warum?
          if(!TrySendElsterCanFrame(sendElsterFrame2, out responseFrame, true))
            return false;

          if(responseFrame?.Value == null)  
            return false;
          
          elsterValue.SetNextMultiValue(responseFrame.Value);
        }
        return true;
        // HeatingModule ->Respond on ExternalDevice WAERMEERTRAG_HEIZ_TAG_KWH et_double_val:32
        // HeatingModule ->Respond on ExternalDevice WAERMEERTRAG_HEIZ_TAG_WH 635 (0x027B)
        // RequestElsterValue: ExternalDevice ->Read on HeatingModule WAERMEERTRAG_HEIZ_TAG_KWH  => et_double_val:32
    }

    public bool RequestElsterValue(ushort senderCanId, ushort receiverCanId, ushort elster_idx, out ElsterValue? returnElsterValue)
    {
      if(senderCanId > 0x7FF) senderCanId = _heatingAdapterConfig.StandardSenderCanID;
      ElsterCANFrame  sendElsterFrame = new ElsterCANFrame(
                                      senderCanId,
                                      (ElsterModule)receiverCanId,
                                      ElsterTelegramType.Read,
                                      elster_idx,
                                      0);

        ElsterCANFrame? responseFrame;
        if( TrySendElsterCanFrame(sendElsterFrame, out responseFrame, true) == true)
        {
          if(responseFrame?.Value != null)
          {
            returnElsterValue = responseFrame.Value;
            if( responseFrame.Value.ValueType == ElsterValueType.et_double_val || 
                responseFrame.Value.ValueType == ElsterValueType.et_triple_val)
            {
                  if (responseFrame.Value.IsElsterNullValue() == false)
                  {
                    ElsterValue tempElsterValue = responseFrame.Value;
                    bool ret = RequestMultiValueTelegram(sendElsterFrame, ref tempElsterValue);
                    // Wenn ein Problem bei den Folgetelgrammen auftritt, dann ist der returnElsterValue falsch;
                    if(ret == false){
                      _logger.LogWarning($"RequestElsterValue: {sendElsterFrame.ToString()} => Error in reading or sending of multi value response telegram");
                      return false;
                    } 
                    returnElsterValue = tempElsterValue;
                  }
            }
          }
          else
          {
            returnElsterValue = null;
            _logger.LogWarning($"RequestElsterValue: {sendElsterFrame.ToString()} => Response frame is null or value is null");
            return false;
          }
          // Ein Wert (bzw. mehrer Telegramme) wurden erfolgreich mit einem ElsterValue gelesen
         _logger.LogInformation($"RequestElsterValue: {sendElsterFrame.ToString()} => {returnElsterValue.ToString()}");
          return true;
        }
        else
        {
          _logger.LogWarning($"RequestElsterValue: {sendElsterFrame.ToString()} => No response");
          returnElsterValue = null;
          return false;
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
    private bool TrySendElsterCanFrame(ElsterCANFrame sendElsterCanFrame, out ElsterCANFrame? responseFrame, bool waitForAnswer = false)
    {

        lock(_sendLock)
        {
          // Das ElsterCANFrame zu einem CAN-Bus-Frame konvertieren
          StandardCanFrame sendCanFrame = sendElsterCanFrame.ToCanFrame();
          _logger.LogDebug($"{sendCanFrame.CreatedAt.ToString("dd.MM.yy HH:mm:ss.fff")} <- {sendCanFrame.ToString()}"); 

          for (int tryCount = 1; tryCount <= _heatingAdapterConfig.SendRetryCount; tryCount++)  // 3 Versuche
          {
            if(waitForAnswer) // Wir wollen sofort die Antworten zwischenspeichern
            {
              // Elemente entfernen, bis die Sammlung leer ist
              while (!_responseFramesQueue.IsCompleted && _responseFramesQueue.TryTake(out responseFrame)) 
              {  }
              _storeResponseFramesInQueue = true;
            }
            try 
            {
              // Versuche das CAN-Frame an den CAN-Bus zu senden
              bool ret = _canBusService.SendCanFrame(sendCanFrame);
              if(ret) {

                if(waitForAnswer)
                {
                  DateTime startWaitTime    = DateTime.Now.AddMilliseconds(_heatingAdapterConfig.MaxReceivingWaitTime);
                  TimeSpan currentWaitTime  = startWaitTime - DateTime.Now;
                  ElsterCANFrame? possibleResponseFrame = null;
                  while(_responseFramesQueue.TryTake(out possibleResponseFrame, currentWaitTime ))
                  {
                    if(possibleResponseFrame != null)
                    {
                      if(possibleResponseFrame.IsAnswerToElsterCanFrame(sendElsterCanFrame))
                      {
                        responseFrame = possibleResponseFrame;
                        return true;
                      }
                    }
                    currentWaitTime = startWaitTime - DateTime.Now;  // Restzeit aktualisieren
                  }
                }
                else{
                  responseFrame = null;
                  return true;
                }
              }
            }
            finally
            {
              _storeResponseFramesInQueue = false;
            }
            _logger.LogWarning($"Failed to send frame {sendElsterCanFrame.ToString()} to CAN-Bus, retry {tryCount}/{_heatingAdapterConfig.SendRetryCount}...");
            Thread.Sleep(_heatingAdapterConfig.SendRetryDelay);
          }
        }  
      
        // Nach allen Versuchen konnte das CAN-Frame nicht gesendet werden
        responseFrame = null;
        return false;
    }
/*
bool KCanElster::Send(unsigned count, bool WaitForAnswer, int inner_delay)
{
  EmptyServer();

  for (int i = 0; (unsigned) i < count; i++)  // 3 Versuche
  {
    int wait_delay = 1000; // zwischen 2 Versuchen

    if (Send_Frame())
    {
      if (!WaitForAnswer)
        return true;

      int del = count > 1 ? 400 : inner_delay;
      for (int k = 0; k < del; k++)
      {
        while (Get_Frame())
        {
          bool Ok = false;

          if (RecvFrame.Id == SendFrame.Id) // Echo
            continue;
 
          if (DriverType == NCanUtils::dt_cs)
          {             
            Ok = RecvFrame.Len > 0;
          } else
          if (RecvFrame.IsAnswerToElsterIndex(SendFrame))
            Ok = true;
        #if !defined(__UVR__)
          if (Ok)
            SniffedData.ClearSniffedValue(RecvFrame);
          else
            continue;
        #endif
          return Ok;
        }
        NUtils::SleepMs(1);
        wait_delay--;
      }
    }
    if ((unsigned)(i+1) < count && wait_delay > 0)
      NUtils::SleepMs(wait_delay);
  }

  return false;
}
*/
    public void Dispose()
    {
        _logger.LogInformation("AC10HeatingAdapter disposed.");
        // Beende den Service
    }
}
