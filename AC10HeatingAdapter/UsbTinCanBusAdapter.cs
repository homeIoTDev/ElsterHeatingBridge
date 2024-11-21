using System.Text.RegularExpressions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging;
using System.IO.Ports;
using System.Text;

namespace AC10Service;
// soll UsbTinCanBusAdapter
public partial class UsbTinCanBusAdapter: IDisposable
{
    private readonly ILogger<UsbTinCanBusAdapter>   _logger;
    private readonly UsbTinCanBusAdapterConfig      _config;
    private SerialPort                              _serialPort;
    private System.Timers.Timer                     _openPortTimer;
    private readonly AC10HeatingAdapter             _ac10HeatingAdapter;
    private Action<string,string>?                  _sendReadingCallback;
    private bool                                    _ignoreCanBusErrors = false;
    private CanAdapterResponse                      _lastCanAdapterResponse;
    private readonly ManualResetEventSlim           _sendLineResetEvent = new ManualResetEventSlim(false);
    private readonly object                         _sendLineLock       = new object();

    public enum CanAdapterResponse { OK, Error, Timeout };

    public UsbTinCanBusAdapter(IOptions<UsbTinCanBusAdapterConfig> config, ILogger<UsbTinCanBusAdapter> logger)
    {
        _config         = config.Value;
        _logger         = logger;

        _logger.LogInformation("UsbTinCanBusAdapter initialized with configuration.");
        _openPortTimer  = new System.Timers.Timer(10000);
        _serialPort     = new SerialPort();
        ConfigureSerialPort();
        _ac10HeatingAdapter = new AC10HeatingAdapter(_logger);

    }

    public void Start(Action<string,string> sendReadingCallback)
    {
        _logger.LogInformation(
            $"Starting UsbTinCanBusAdapter for port {_serialPort.PortName} " +
            $"{_serialPort.BaudRate},{_serialPort.DataBits},{_serialPort.Parity},{_serialPort.StopBits}..." );
        _sendReadingCallback        = sendReadingCallback;

        _openPortTimer.Elapsed      += (sender, e) => OpenPort();
        _openPortTimer.AutoReset    = true;
        _openPortTimer.Start();
        _sendReadingCallback?.Invoke("CAN_Channel", "undefined");        
    }

    public void Stop()
    {
        try
        {
            _logger.LogInformation("UsbTinCanBusAdapter stopping...");
            _openPortTimer?.Stop();
            _openPortTimer?.Dispose();
            if (_serialPort?.IsOpen == true)
            {
                _logger.LogInformation("Closing serial port {_serialPort.PortName}.", _serialPort.PortName);
                _serialPort?.Close();
            }
            else
            {
                _logger.LogInformation("Serial port {_serialPort.PortName} is already closed.", _serialPort?.PortName);
            }
            _sendReadingCallback?.Invoke("CAN_Channel", "closed");
            _serialPort?.Dispose();

        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Stopping UsbTinCanBusAdapter failed.");
        }
    }


    private void ConfigureSerialPort()
    {
        try
        {
            if (_serialPort.IsOpen)
            {
                _serialPort.Close();
            }

            _serialPort.PortName   = _config.PortName;
            _serialPort.BaudRate   = _config.BaudRate;
            _serialPort.DataBits   = (int)_config.DataBits;
            _serialPort.Parity     = (Parity)_config.Parity;
            _serialPort.StopBits   = (StopBits)_config.StopBits;
            _serialPort.Handshake  = (Handshake)_config.Handshake;
            _serialPort.NewLine    = "\r";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Could not configure serial port.");
        }
    }

    private readonly SemaphoreSlim readLinesFromPortStartedSemaphore = new SemaphoreSlim(0);

    private void OpenPort()
    {
        if (_serialPort.IsOpen)
        {
            return;
        }

        try
        {
            _serialPort.Open();
            _logger.LogInformation($"Successfully opened serial port {_config.PortName}.");
        }
        catch (System.UnauthorizedAccessException ex)
        {
            _logger.LogError(ex, $"Could not open serial port {_config.PortName} because of insufficient permissions. Please run 'sudo usermod -aG dialout $USER' or check device permissions and try again.");
            return;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Could not open serial port {_config.PortName}. Trying again in 10 seconds.");
            return;
        }


        if (_serialPort.IsOpen)
        {
            Task readLinesTask = Task.Run(() => { ReadLinesFromPort();});
            readLinesFromPortStartedSemaphore.Wait();
            if(_sendReadingCallback!=null)
            {
                _ac10HeatingAdapter.Start(SendLineWithoutResponse, _sendReadingCallback);
            }
            else
            {    
                _logger.LogError($"Could not start USBtinCanBusAdapter because _sendReadingCallback is null.");
            }
            Start_CANBusInit();

            
        }
    }

    private void Start_CANBusInit()
    {
        _logger.LogInformation("Starting CANBusInit...");
        _sendReadingCallback?.Invoke("CAN_Channel", "config mode");
        _ignoreCanBusErrors = true;
        SendLine(""); // clear CanAdapter-buffer. 2x
        SendLine(""); // clear CanAdapter-buffer. 2x
        SendLine("C"); // close CAN-Bus channel, if open.
        Task.Delay(200);
        SendLine(""); // clear CanAdapter-buffer. 2x
        _ignoreCanBusErrors = false;
        SendLine("V"); // get HW Version
        SendLine("v"); // get SW version
        SendLine("S1"); // setup standard CAN bit-rate 20kBit(S1)

        // I want to use the hardware filter of the can controller to accept only can telegrams adressed to me
        // This results in way lower traffic and system load.
        // Set acceptance filter and mask with "M" and "m" command does not work, because it is restricted to SJA1000 format with only the first 11bit relevant.
        // This will not work with adapters based upon Siemens SJA1000, as registers of MCP2515 are set directly in the adapter.
        //SendLine("M00000000000\r"); // set acceptance filter
        //SendLine("m00000000000\r"); // set acceptance mask

        SendLine("O"); // open CAN-Bus channel
        SendLine("F"); // get Error state
        _sendReadingCallback?.Invoke("CAN_Channel", "opened");    
    }

    private void SendLineWithoutResponse(string line)
    {
        _ = SendLine(line);
    }

    private CanAdapterResponse? SendLine(string line)
    {
        string lineForLogging = EscapeControlCharacters(line+"\r");
        if (_serialPort.IsOpen)
        {
            lock(_sendLineLock)
            {
                _logger.LogDebug($"Tx serial port: '{lineForLogging}' length: {line.Length}");    
                _serialPort.WriteLine(line);
                _sendLineResetEvent.Reset();
                if(_sendLineResetEvent.Wait(300)==false)
                {
                    SetCanAdapterResponse(CanAdapterResponse.Timeout);
                }
                _logger.LogDebug($"Response to '{lineForLogging}': {_lastCanAdapterResponse}");
                return _lastCanAdapterResponse;
            }
        }
        else
        {
            _logger.LogError($"Could not send line '{lineForLogging}' because serial port {_config.PortName} is not open.");
        }
        return null;
    }

    private void ReadLinesFromPort()
    {
        _logger.LogInformation($"Reading from serial port {_config.PortName}...");
        readLinesFromPortStartedSemaphore.Release();
        try
        {
            while (_serialPort.IsOpen)
            {
                var lineBuilder = new StringBuilder();
                while (_serialPort.IsOpen)
                {
                    int c = _serialPort.ReadChar();
                    if (c == 13)
                    {
                        break; //line complete
                    }
                    else if( c == 7)    //beel 
                    {
                        lineBuilder.Append(c);    // when <bell> found add to line, to be able to process errors in the main loop
                        SetCanAdapterResponse(CanAdapterResponse.Error);
                        break; //line complete
                    }
                    lineBuilder.Append(c);
                }
                //Complete String
                string line = lineBuilder.ToString();


                if(line.Length==0)
                {
                    SetCanAdapterResponse(CanAdapterResponse.OK);
                }
                else if(line.Length==1)
                {
                    if( line[0] == (char)7)    //beel 
                    {
                        SetCanAdapterResponse(CanAdapterResponse.Error);
                    }
                    else if( line.ToUpper()=="Z")   
                    {
                        SetCanAdapterResponse(CanAdapterResponse.OK);
                    }
                }
                _logger.LogDebug($"Rx serial port: '{line}'");
                _ac10HeatingAdapter.ReceiveLine(line);
            }
            _logger.LogInformation($"Reading from serial port {_config.PortName} stopped, serial port is closed.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Could not read from serial port {_config.PortName}");
            try { _serialPort.Close(); } catch { }  // Close serial port in case of error
        }

        _logger.LogInformation($"Reading from serial port {_config.PortName} stopped");
    }

    private void SetCanAdapterResponse(CanAdapterResponse response)
    {
        _lastCanAdapterResponse = response;
        _sendLineResetEvent.Set();
    }

    [GeneratedRegex(@"[\r\n\t\b\f\a\v]")]
    private static partial Regex ControlCharactersRegex();

    private static string EscapeControlCharacters(string input)
    {
        return ControlCharactersRegex().Replace(input, match =>
        {
            switch (match.Value)
            {
                case "\r": return "\\r";
                case "\n": return "\\n";
                case "\t": return "\\t";
                case "\b": return "\\b";
                case "\f": return "\\f";
                case "\a": return "\\a";
                case "\v": return "\\v";
                default: return match.Value;
            }
        });
    }

    public void Dispose()
    {
        _logger.LogInformation("AC10HeatingAdapter disposed.");
        // Beende den Service
    }
}
