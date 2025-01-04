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

    /// <summary>
    /// Requests multiple telegrams for one elster value. The function assumes that the first
    /// value has already been received and is in <paramref name="elsterValue"/>.
    /// It then requests the next elster value(s) with an decreasing elster index
    /// and adds them to the <paramref name="elsterValue"/>.
    /// </summary>
    /// <param name="sendFrame">The frame that was used to request the first value.</param>
    /// <param name="elsterValue">The Elster value that should be completed with the next value(s).</param>
    /// <returns>true if all values were successfully requested and added to <paramref name="elsterValue"/>.</returns>
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


    /// <summary>
    /// Requests an elster value per read telegram. The function tries to add missing values for double- or triple-values.
    /// </summary>
    /// <param name="senderCanId">The sender CAN-ID or a value > 0x7FF for the default sender CAN-ID.</param>
    /// <param name="receiverCanId">The receiver CAN-ID as <see cref="ElsterModule"/>.</param>
    /// <param name="elster_idx">The elster index of the value to read.</param>
    /// <param name="returnElsterValue">The requested elster value.</param>
    /// <returns>true if the telegram was successfully sent and the value(s) were received and correctly interpreted.</returns>
    public bool RequestElsterValue(ushort senderCanId, ushort receiverCanId, ushort elster_idx, out ElsterValue? returnElsterValue)
    {
      if(senderCanId > 0x7FF) senderCanId = _heatingAdapterConfig.StandardSenderCanID;
      ElsterCANFrame  sendElsterFrame = new ElsterCANFrame(
                                      senderCanId,
                                      (ElsterModule)receiverCanId,
                                      ElsterTelegramType.Read,
                                      elster_idx,
                                      0);

        if( TrySendElsterCanFrame(sendElsterFrame, out ElsterCANFrame? responseFrame, true) == true)
        {
          if(responseFrame?.Value != null)
          {
            returnElsterValue = responseFrame.Value;
            if( (responseFrame.Value.ValueType == ElsterValueType.et_double_val || 
                 responseFrame.Value.ValueType == ElsterValueType.et_triple_val) && 
                responseFrame.Value.IsElsterNullValue() == false)
            {
              if(!RequestMultiValueTelegram(sendElsterFrame, ref returnElsterValue))
              {
                _logger.LogWarning($"RequestElsterValue: {sendElsterFrame.ToString()} => Error in the process of retrieving the remaining values for a composite multi-value via telegram");
                return false;
              }
            }
            _logger.LogInformation($"RequestElsterValue: {sendElsterFrame.ToString()} => {returnElsterValue.ToString()}");
            return true;
          }
          else
          {
            _logger.LogWarning($"RequestElsterValue: {sendElsterFrame.ToString()} => Response frame is null or value is null. Error in the process of retrieving an value via elster read telegram");
          }
        }
        else
        {
          _logger.LogWarning($"RequestElsterValue: {sendElsterFrame.ToString()} => No response");
        }
        returnElsterValue = null;
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
