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
    /// List of valid Elster modules with their Device-ID. The Device-IDs can be used to start communication with the module
    /// in a configuration mode, similar to how it is done in CS. This list is only filled by <see cref="ScanElsterModules"/>()
    /// </summary>
    private static List<(ElsterModule canId, ElsterValue deviceId, ElsterValue? softwareNumber, ElsterValue? softwareVersion )> Modules = 
    new List<(ElsterModule canId, ElsterValue deviceId, ElsterValue? softwareNumber, ElsterValue? softwareVersion )>();

    public void ScanElsterModules(ushort senderCanId = 0xFFF) 
    {
      try {
        if(senderCanId > 0x7FF) senderCanId = Convert.ToUInt16(_heatingAdapterConfig.StandardSenderCanID, 16);
      }
      catch {
        senderCanId = 0x700;
      }
      _logger.LogInformation("Scanning for Elster modules...");
      Modules.Clear();
      StringBuilder logString = new StringBuilder();
      logString.AppendLine($"scan on CAN-id: {senderCanId:X3}");
      logString.AppendLine("list of valid can id's:");
      logString.AppendLine("");
      logString.AppendLine("");
      
      const int MODULES_PER_GROUP = 0x80; // 128 Geräte pro Gruppe
      for (int recv = 0; recv < 0x10; recv++) 
          if (recv != senderCanId / 0x80) // die SenderCanId wird nicht gescannt, das sind wir selbst
          {
              int foundDevicesCount = 0;
              for (int i = 0; i < 0x10; i++) // 0x10 = 16 Geräte vom gleichen Typ (wie z.B. FEK, FEK2 usw.) werden gescannt
              {
                ushort curModuleId = (ushort)(MODULES_PER_GROUP * recv + i);

                ElsterValue? softwareNumber       = null;
                ElsterValue? softwareVersion      = null;
                ElsterValue? deviceConfiguration2 = null;
                
                if (RequestElsterValue(senderCanId, curModuleId, ElsterTabIndexName["GERAETE_ID"], out ElsterValue? deviceId))
                {
                  if (deviceId?.IsElsterNullValue()==true)
                  {
                      _= RequestElsterValue(senderCanId, curModuleId, ElsterTabIndexName["SOFTWARE_NUMMER"], out softwareNumber);
                      _= RequestElsterValue(senderCanId, curModuleId, ElsterTabIndexName["SOFTWARE_VERSION"], out softwareVersion);
                      if( curModuleId == (int)ElsterModule.Direct)
                      {
                         _= RequestElsterValue(senderCanId, curModuleId, ElsterTabIndexName["GERAETEKONFIGURATION_2"], out deviceConfiguration2);
                      }
                  }
                  foundDevicesCount++;

                  Modules.Add(((ElsterModule)curModuleId, deviceId!, softwareNumber, softwareVersion));

                  logString.AppendFormat("  {0:X3} ({1:X4} = ", 0x80*recv + i, deviceId?.GetShortValue());

                  if (softwareVersion?.IsElsterNullValue() == false)
                    logString.AppendFormat("{0}-{1:D2})\n", (softwareNumber?.GetValue()), softwareVersion?.GetShortValue() & 0xff);
                  else
                  {
                    //Only valid for Direct module
                    if( deviceConfiguration2?.IsElsterNullValue() == false)
                    {
                      ushort? swNumber = (ushort?)deviceConfiguration2?.GetLittleEndianValue();
                      logString.AppendFormat("{0}-{1:D2})\n", swNumber>> 8, swNumber & 0xff);
                    }
                    else // 
                    {
                      logString.Append(deviceId?.ToString()+")\n");
                    }
                  }
                }
                if (foundDevicesCount < i-2) // abbrechen, wenn die Anzahl der gefundenen Geräte 
                  break;
              }
          }
          _logger.LogInformation(logString.ToString());
          _logger.LogInformation("Scanning for Elster modules finished");
          foreach(var module in Modules)
          {
            _logger.LogInformation(
                   $"Found Elster module: {module.canId.ToString().PadLeft(15)} " +
                   $"({((ushort)module.canId).ToString("X3").PadRight(3)}) = " +
                   $"Device-ID: {module.deviceId.GetShortValue(),6:X4} | " + 
                   $"SW-Nr: {(module.softwareNumber?.IsElsterNullValue() == false ? module.softwareNumber.GetShortValue()?.ToString().PadRight(6) : "N/A".PadRight(6))} | " +
                   $"SW-Ver: {(module.softwareVersion?.IsElsterNullValue() == false ? 
                      $"{(ushort?)module.softwareVersion.GetShortValue()}".PadRight(6) 
                      : "N/A".PadRight(6))}");
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
      try {
        if(senderCanId > 0x7FF) senderCanId = Convert.ToUInt16(_heatingAdapterConfig.StandardSenderCanID, 16);
      }
      catch {
        senderCanId = 0x700;
      }

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

    public void Dispose()
    {
        _logger.LogInformation("AC10HeatingAdapter disposed.");
        // Beende den Service
    }
}