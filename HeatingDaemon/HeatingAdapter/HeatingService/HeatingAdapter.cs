using System;
using System.Data;
using System.Text;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Collections.Concurrent;
using System.Threading;
using static HeatingDaemon.KElsterTable;

namespace HeatingDaemon;

public class HeatingAdapter : IDisposable, IHeatingService
{
    private readonly ILogger<HeatingAdapter>        _logger;
    private ICanBusService                          _canBusService;
    private IMqttService                            _mqttService;
    private bool                                    _storeResponseFramesInQueue = false;
    private readonly BlockingCollection<ElsterCANFrame>_responseFramesQueue = new(new ConcurrentQueue<ElsterCANFrame>());
    private object                                  _sendLock = new();
    private HeatingAdapterConfig                    _heatingAdapterConfig;
    private List<CyclicReadingQueryDto>             _cyclicReadingQueryList = new();
    private ushort                                  _standardSenderCanID = 0x700;
    private bool                                    _passiveElsterTelegramsEnabled = false;
    private static readonly Dictionary<(ushort ElsterIndex, uint SenderCanId, uint ReceiverCanId),(long count, ElsterCANFrame frame)> _passiveElsterTelegramList = new();

    public HeatingAdapter(IOptions<HeatingAdapterConfig> heatingAdapterConfig,
                          ICanBusService canBusService,
                          IMqttService mqttService,
                          ILogger<HeatingAdapter> logger)
    {
      _heatingAdapterConfig = heatingAdapterConfig.Value;
      _canBusService        = canBusService;
      _mqttService          = mqttService;
      _logger               = logger;
      try { _standardSenderCanID = Convert.ToUInt16(_heatingAdapterConfig.StandardSenderCanID, 16);} catch {}
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

        if(elsterFrame != null && elsterFrame.IsValidTelegram && elsterFrame.Value !=null) {

          //Get count of passive Elster Telegrams 
          if(_passiveElsterTelegramsEnabled &&
              elsterFrame.TelegramType != ElsterTelegramType.Read)
          {
            var passiveElsterTelegramKey = (elsterFrame.ElsterIndex, elsterFrame.SenderCanId, elsterFrame.ReceiverCanId);
            if(_passiveElsterTelegramList.TryGetValue(passiveElsterTelegramKey, out var passiveElsterTelegram)==true)
            {
              var passiveElsterTelegramNew = (passiveElsterTelegram.count + 1, elsterFrame);
              _passiveElsterTelegramList[passiveElsterTelegramKey] = passiveElsterTelegramNew;
            }
            else
            { 
              _passiveElsterTelegramList.Add(passiveElsterTelegramKey, (1, elsterFrame));
            }
          } // Nur Telegramme mit Werten

          foreach(CyclicReadingQueryDto cyclicReadingQuery in _cyclicReadingQueryList)
          {
            if ( cyclicReadingQuery.ElsterIndex == elsterFrame.ElsterIndex )
            {
              if 
                (
                
                  (cyclicReadingQuery.Schedule == ScheduleType.Passive &&
                    (uint)cyclicReadingQuery.ReceiverCanId == elsterFrame.ReceiverCanId  && 
                    (((ushort)cyclicReadingQuery.SenderCanId > 0x7FF) || 
                    (uint)cyclicReadingQuery.SenderCanId == elsterFrame.SenderCanId)) ||

                  (cyclicReadingQuery.Schedule != ScheduleType.Passive &&
                    (uint)cyclicReadingQuery.ReceiverCanId == elsterFrame.SenderCanId  && 
                    (((ushort)cyclicReadingQuery.SenderCanId > 0x7FF &&
                    _standardSenderCanID == elsterFrame.ReceiverCanId) || 
                    (uint)cyclicReadingQuery.SenderCanId == elsterFrame.ReceiverCanId))
                  )
                
            {
              _logger.LogDebug($"CyclicReadingQuery {cyclicReadingQuery.ReadingName} with value '{elsterFrame.Value.ToString()}' is being collected");
              _mqttService.SetReading(cyclicReadingQuery.ReadingName, 
                                      elsterFrame.Value.ToString(),
                                      cyclicReadingQuery.SendCondition == SendCondition.OnEveryRead);
            }
            }
          }
          
          if(elsterFrame.IsKnownElsterIndex)
          {
            _logger.LogDebug($"{elsterFrame.ToString()}");
          }
          else
          {
            _logger.LogWarning($"{elsterFrame.ToString()}");
          }
        }
    }

    public void CyclicReadingLoop( CancellationToken cts, List<CyclicReadingQueryDto> readingList)
    {
        _cyclicReadingQueryList = readingList;

        while (!cts.IsCancellationRequested) 
        {
          if(_canBusService.IsCanBusOpen)
          {
            foreach (CyclicReadingQueryDto cyclicReadingQuery in _cyclicReadingQueryList)
            {
                if(cts.IsCancellationRequested) break;
                if(_canBusService.IsCanBusOpen == false) break;

                if( ( cyclicReadingQuery.Schedule == ScheduleType.AtStartup && 
                      cyclicReadingQuery.LastReadTime == DateTime.MinValue) ||
                      cyclicReadingQuery.Schedule == ScheduleType.Periodic)
                {
                    // Read the value if the interval is reached
                    if(cyclicReadingQuery.LastReadTime.AddSeconds(cyclicReadingQuery.Interval.Seconds) < DateTime.Now)
                    {
                        if(cyclicReadingQuery.Operation == OperationType.GetElsterValue)
                        {
                            _logger.LogDebug($"CyclicReadingQuery requesting value for {cyclicReadingQuery.ReadingName}");
                            if(RequestElsterValue(
                                (ushort)cyclicReadingQuery.SenderCanId,
                                (ushort)cyclicReadingQuery.ReceiverCanId,
                                cyclicReadingQuery.ElsterIndex,
                                out var elsterValue)==true)
                            {
                                cyclicReadingQuery.LastReadTime = DateTime.Now;
                            }
                        }
                    }
                }
            }
          }
          Thread.Sleep(1000);  // the minimum loop time of 1 second  
        }
    }

    
    /// <summary>
    /// Enables or disables the collection of passive Elster Telegrams. 
    /// If the collection is enabled, the method prints the list of collected passive Elster Telegrams, 
    /// sorted by the count of the telegram in descending order.
    /// </summary>
    public void PrintPassiveElsterTelegramList()
    {
      if(_passiveElsterTelegramsEnabled == false)
      {
        _passiveElsterTelegramsEnabled = true;
        _logger.LogInformation("Collecting of passive Elster Telegrams enabled. Press 'P' to disable.");
      }
      else
      {
        _passiveElsterTelegramsEnabled = false;
        _logger.LogInformation("Passive Elster Telegrams:");
        var sortedList = _passiveElsterTelegramList.OrderByDescending(x => x.Value.count).ToList();
        foreach (var item in sortedList)
        {
          _logger.LogInformation($"  {item.Value.count}x  {item.Value.frame.ToString()}");
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

      if(senderCanId > 0x7FF) senderCanId = _standardSenderCanID;

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
          //Log all found modules in old style (and not in one line, becouse the console logger does not support that)
          var lines = logString.ToString().Split('\n');
          foreach (var line in lines)
          {
            _logger.LogInformation(line);
          }

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

    
    public void CanScanElsterIndex(ElsterModule senderCanID, ElsterModule receiverCanID, ushort? elsterIndex, ushort? elsterValue)
    {
      if(elsterIndex!=null && elsterValue!=null)
      {
        //Write Elster Value
        _logger.LogInformation($"------------------------------------------");  
        bool ret = WriteElsterValue((ushort)senderCanID, (ushort)receiverCanID, elsterIndex.Value, elsterValue.Value);
        _logger.LogInformation($"Write Elster Value '{elsterValue:x4}' to {receiverCanID} was successfully sent: {ret}");  
        _logger.LogInformation($"------------------------------------------");  
      }
      else if(elsterIndex!=null && elsterValue==null)
      {
        //Read Elster Value
        if(RequestElsterValue((ushort)senderCanID, (ushort)receiverCanID, (ushort)elsterIndex, out ElsterValue? retValue))
        {
          _logger.LogInformation($"------------------------------------------");  
          _logger.LogInformation($"Read Elster Value '{retValue?.ToString()}'");  
          _logger.LogInformation($"   in hex:'{retValue?.ToHexString()}'");  
          _logger.LogInformation($"------------------------------------------"); 
        } 
        else
        {
          _logger.LogInformation($"Read Elster Value failed");  
        }
      }
      else 
      {
        //Read all Elster Values on receiverCanId
        RequestAllElsterValues((ushort)senderCanID, (ushort)receiverCanID,out var returnList);
        _logger.LogInformation($"------------------------------------------");
        _logger.LogInformation($"Read all Elster Values on {receiverCanID}");
        foreach(var retValue in returnList)
        {
          _logger.LogInformation($"{retValue.retString}");
        }
        _logger.LogInformation($"------------------------------------------");
      }
    }

    private void RequestAllElsterValues(ushort senderCanId, ushort receiverCanId, out List<(ElsterValue value, ushort elster_index, string retString)> elsterValues)
    {
        elsterValues = new List<(ElsterValue value, ushort elster_index, string retString)>();

        for (int elster_idx = 0; elster_idx <= ushort.MaxValue; elster_idx++)
        {
          int ind = KElsterTable.ElsterTabIndex[elster_idx];
          if (ind >= 0)  // Wenn es ein Elster-Eintrag gibt
          {
            if(RequestElsterValue(senderCanId, receiverCanId, (ushort)elster_idx, out ElsterValue? retValue))
            {
              if( retValue == null) continue;
              if( retValue.IsElsterNullValue()) continue;

              StringBuilder retString = new StringBuilder();
              retString.Append($"  {{ 0x{receiverCanId:X3}, 0x{elster_idx:X4}, 0x{retValue.ToHexString()}}},");
              var elsterEntry = KElsterTable.ElsterTable[ind];
              retString.AppendLine($"  // {elsterEntry.Name}: {retValue.ToString()}");

              elsterValues.Add((retValue, (ushort)elster_idx, retString.ToString()));
            }
          }
       }//for
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
   
    public bool WriteElsterValue(ushort senderCanId, ushort receiverCanId, ushort elster_idx, ushort elsterValue, bool waitForAnswer=false)
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
                                      waitForAnswer?ElsterTelegramType.WriteRespond:ElsterTelegramType.Write,
                                      elster_idx,
                                      elsterValue);

      if( TrySendElsterCanFrame(sendElsterFrame, out ElsterCANFrame? responseFrame, waitForAnswer) == true)
      {
        // Wenn wir nicht auf antwort warten, dann sind wir fertig
        if( !waitForAnswer) return true;

        if(responseFrame?.Value != null)
        {
          //Antwort auf Write Telegram hat ein Value und ist nicht Null
          short? retElsterValueShort = responseFrame.Value.GetShortValue();
          if( retElsterValueShort != null)
          {
            if(retElsterValueShort == elsterValue)
            {
              _logger.LogInformation($"WriteElsterValue: {sendElsterFrame.ToString()} => {responseFrame.Value.ToString()}");
              return true;
            }
            else  
            {
              _logger.LogWarning($"WriteElsterValue: {sendElsterFrame.ToString()} => not the same as requested value {responseFrame.Value.ToString()}");
              return false;
            }
          }
          else
          {
            _logger.LogInformation($"WriteElsterValue: {sendElsterFrame.ToString()} => empty response value: {responseFrame.Value.ToString()}");
            return false;
          }
        }
        else
        {
          _logger.LogWarning($"WriteElsterValue: {sendElsterFrame.ToString()} => Response frame is null or value is null. Error in the process of retrieving an value via elster read telegram");
        }
      }
      else
      {
        _logger.LogWarning($"WriteElsterValue: {sendElsterFrame.ToString()} => No response");
      }
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
        _logger.LogInformation("HeatingAdapter disposed.");
        // Beende den Service
    }

}
