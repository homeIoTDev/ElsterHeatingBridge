using System;
using System.Data;
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
