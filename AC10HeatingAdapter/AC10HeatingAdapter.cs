using System;
using System.Data;
using System.Text;
using Microsoft.Extensions.Logging;
using static AC10Service.KElsterTable;

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

        string toDeviceModule   = Enum.IsDefined(typeof(ElsterModule), (int)frame.ReceiverCanId) ? frame.ReceiverElsterModule.ToString() : $"{frame.ReceiverCanId:X3}";
        string fromDeviceModule = Enum.IsDefined(typeof(ElsterModule), (int)frame.SenderCanId) ? frame.SenderElsterModule.ToString() : $"{frame.SenderCanId:X3}";
        short elsterIndex = frame.GetElsterIdx();
        if (elsterIndex < 0)
            return false;
        int ind = KElsterTable.ElsterTabIndex[elsterIndex];
        if (ind < 0)
        {
            _logger.LogError($"Elster {frame.TelegramType} CAN frame from {fromDeviceModule} with elster index {elsterIndex:X4} not found, with possible data: {frame.GetValue()} frame: {frame}");
            return false;
        }
        var elsterEntry = KElsterTable.ElsterTable[ind];
        string elsterValue = "= "+ KElsterTable.GetValueString(elsterEntry.Type, (short)frame.GetValue());
        //If this is a request, then the value is always 0 and also unimportant, as it is being requested
        if(frame.TelegramType == ElsterTelegramType.Read) {
            elsterValue = "";
        }
        _logger.LogDebug($"{fromDeviceModule} ->{frame.TelegramType} {toDeviceModule} {elsterEntry.Name} {elsterValue}");
        
        return true;
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
