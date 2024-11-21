using System;
using System.Data;
using Microsoft.Extensions.Logging;

namespace AC10Service;
//soll AC10HeatingAdapter
internal class AC10HeatingAdapter
{
    private readonly ILogger<UsbTinCanBusAdapter> _logger;

    private Action<string>?         _sendLineCallback;
    private Action<string,string>?  _sendReadingCallback;

    public AC10HeatingAdapter(ILogger<UsbTinCanBusAdapter> logger)
    {
        _logger = logger;
    }

    public void ReceiveLine(string line)
    {
    
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
        _sendReadingCallback?.Invoke("CAN_Channel", "config mode");


        SendLine("C\r"); // close CAN-Bus channel, if open.
        SendLine("V\r"); // get HW Version
        SendLine("v\r"); // get SW version
        //SendLine("X1\r"); // set auto mode for can232, not required for USBtin which responds with "err"
        SendLine("S1\r"); // set Baudrate

        // I want to use the hardware filter of the can controller to accept only can telegrams adressed to me
        // This results in way lower traffic and system load.
        // Set acceptance filter and mask with "M" and "m" command does not work, because it is restricted to SJA1000 format with only the first 11bit relevant.
        // This will not work with adapters based upon Siemens SJA1000, as registers of MCP2515 are set directly in the adapter.
        //SendLine("M00000000000\r"); // set acceptance filter
        //SendLine("m00000000000\r"); // set acceptance mask


        SendLine("O\r"); // open CAN-Bus channel
        SendLine("F\r"); // get Error state
        _sendReadingCallback?.Invoke("CAN_Channel", "opened");        
    }
}
