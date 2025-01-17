using System.Text.RegularExpressions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging;
using System.IO.Ports;
using System.Text;

namespace HeatingDaemon;

public partial class UsbTinCanBusAdapter: IDisposable, ICanBusService
{
    private readonly ILogger<UsbTinCanBusAdapter>   _logger;
    private readonly UsbTinCanBusAdapterConfig      _config;
    private SerialPort                              _serialPort;
    private System.Timers.Timer                     _openPortTimer;
    private Lazy<IHeatingService>                   _heatingService;
    private IMqttService                            _mqttService;
    public bool                                     IsCanBusOpen{ get; private set; } = false;
    private CanAdapterResponse                      _lastCanAdapterResponse;
    private readonly ManualResetEventSlim           _sendLineResetEvent = new ManualResetEventSlim(false);
    private readonly object                         _sendLineLock       = new object();

    public enum CanAdapterResponse { OK, Error, Timeout };

    public UsbTinCanBusAdapter( IOptions<UsbTinCanBusAdapterConfig> config,
                                Lazy<IHeatingService> heatingService,
                                IMqttService mqttService,
                                ILoggerFactory loggerFactory)
    {
        _config         = config.Value;
        _logger         = loggerFactory.CreateLogger<UsbTinCanBusAdapter>();
       
        _logger.LogInformation("UsbTinCanBusAdapter initialized with configuration.");
        _heatingService = heatingService;
        _mqttService    = mqttService;
        _openPortTimer  = new System.Timers.Timer(10000);
        _serialPort     = new SerialPort();
        ConfigureSerialPort();

    }


    public void Start()
    {
        _logger.LogInformation(
            $"Starting UsbTinCanBusAdapter for port {_serialPort.PortName} " +
            $"{_serialPort.BaudRate},{_serialPort.DataBits},{_serialPort.Parity},{_serialPort.StopBits}..." );

        _openPortTimer.Elapsed      += (sender, e) => OpenPort();
        _openPortTimer.AutoReset    = true;
        _openPortTimer.Start();
        _mqttService.SetReading("CAN_Channel", "undefined");        
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
            _mqttService.SetReading("CAN_Channel", "closed");
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

            Start_CANBusInit(); 
        }
    }

    /// <summary>
    /// Resets the can bus adapter and closes the serial port. 
    /// Due a the timer of 10 seconds the serial port will be opened again.
    /// </summary>
    public void Reset()
    {
        _logger.LogInformation("Resetting UsbTinCanBusAdapter.");
        try { _serialPort?.Close(); } catch { }  // Close serial port in case of error
        IsCanBusOpen = false;
        _mqttService.SetReading("CAN_Channel", "undefined");
    }


    private void Start_CANBusInit()
    {
        _logger.LogInformation("Starting CANBusInit...");
        _mqttService.SetReading("CAN_Channel", "config mode");
        SendLine(""); // clear CanAdapter-buffer. 2x
        SendLine(""); 
        SendLine("C"); // close CAN-Bus channel, if open.
        Thread.Sleep(200);
        SendLine("V"); // get HW Version --> return Vxxxxyyyy\r
        SendLine("S1"); // setup standard CAN bit-rate 20kBit(S1)

        // I want to use the hardware filter of the can controller to accept only can telegrams adressed to me
        // This results in way lower traffic and system load.
        // Set acceptance filter and mask with "M" and "m" command does not work, because it is restricted to SJA1000 format with only the first 11bit relevant.
        // This will not work with adapters based upon Siemens SJA1000, as registers of MCP2515 are set directly in the adapter.
        //SendLine("M00000000000\r"); // set acceptance filter
        //SendLine("m00000000000\r"); // set acceptance mask

        if(SendLine("O")==CanAdapterResponse.OK) // open CAN-Bus channel
        {
            IsCanBusOpen = true;
            _mqttService.SetReading("CAN_Channel", "opened"); 
            SendLine("F"); // get Error state
        }
        else
        {
            Reset();
            return;
        }
    }

    public bool SendCanFrame(CanFrame frame)
    {
        string line = frame.ToUsbTinString();
        CanAdapterResponse? response = SendLine(line);
        return (response!=null)?(response == CanAdapterResponse.OK):false;
    }

    public void SendLineWithoutResponse(string line)
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
                _logger.LogDebug($"Tx serial port: '{lineForLogging}' length: {line.Length+1}");    // add length + 1, because line ends with \r
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
            //Main Loop of reading from serial port
            while (_serialPort.IsOpen)
            {
                //-=Read Line or Error
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
                        lineBuilder.Append((char)c);    // when <bell> found add to line, to be able to process errors in the main loop
                        SetCanAdapterResponse(CanAdapterResponse.Error);
                        break; //line complete
                    }
                    lineBuilder.Append((char)c);
                }
                //Complete String of one line
                string line             = lineBuilder.ToString();
                string lineForLogging   = EscapeControlCharacters(line);
                CanFrame?  canFrame     = null;                      
                
                //-=Process Line of usbtin commands and responses
                if(line.Length==0)
                {
                    SetCanAdapterResponse(CanAdapterResponse.OK);
                }
                else if(line.Length==1)
                {
                    if(line[0] == (char)7)    //beel 
                    {
                        SetCanAdapterResponse(CanAdapterResponse.Error);
                    }
                    else if( line.ToUpper()=="Z")   
                    {
                        SetCanAdapterResponse(CanAdapterResponse.OK);
                    }
                }
                else if(line.Length==3)
                {
                    if ( line[0] == 'F')  // old version of usbtin use Vxx und vyy in two lines
                    {
                        SetCanAdapterResponse(CanAdapterResponse.OK);
                        if(IsCanBusOpen)
                        {
                            ProcessCanBusErrorResponse(line.Substring(1,2));
                        }
                    }
                }
                else if(line.Length==5)
                {
                    if ( line[0] == 'V')  // old version of usbtin use Vxx und vyy in two lines
                    {
                        SetCanAdapterResponse(CanAdapterResponse.OK);
                        _mqttService.SetReading("HW_Version", line.Substring(1,2));
                        _mqttService.SetReading("SW_Version", line.Substring(3,2));
                    }
                }
                else if(line.Length>0)
                {
                    if (line[0] == 't')
                    {
                        canFrame = StandardCanFrame.ParseFromUsbTin(line.Substring(1));
                    }
                    else if( line[0] == 'T')
                    {
                        canFrame = ExtendedCanFrame.ParseFromUsbTin(line.Substring(1));                       
                    }
                }
                _logger.LogDebug($"Rx serial port: '{lineForLogging}' length: {line.Length}");
                if (canFrame != null)
                {
                    _heatingService.Value.ProcessCanFrame(canFrame);
                }
            } // while(_serialPort.IsOpen)
            _logger.LogInformation($"Reading from serial port {_config.PortName} stopped, serial port is closed.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Could not read from serial port {_config.PortName}");
            Reset();  // Close serial port in case of error
            return;
        }

        _logger.LogInformation($"Reading from serial port {_config.PortName} stopped");
    }

    private void ProcessCanBusErrorResponse(string hexCodeString)
    {
        int errorCode;
        List<string> errorTextArray = new List<string>();
        string errorTextString;
    
        // Retrieve two-digit hex code and convert it to an integer
        errorCode = Convert.ToInt32(hexCodeString, 16);
    
        if ((errorCode & 0x80) != 0) // USBtin Bit 7 - Bus error
        {
            // MCP2515: TXBO=TEC (Transmitt error counter) reaches 255. Error clears after a successful bus recovery sequence
            // SJA1000: BEI = when the CAN controller detects an error on the CAN-bus and the BEIE bit is set
            errorTextArray.Add("Bit 7 - Bus error (BEI)(TXBO>=255 on usbtin)");
        }
        else if ((errorCode & 0x40) != 0)
        {
            // MCP2515: not used
            // SJA1000: Arbitration Lost (ALI)
            errorTextArray.Add("Bit 6 - Arbitration Lost (ALI) - CAN232 only");
        }
        else if ((errorCode & 0x20) != 0)
        {
            // MCP2515: TXEP or RXEP (Transmit/Receive Error-Passive) = TEC or REC is equal to or greater than 128.
            // SJA1000: EPI (Error Passive Interrupt)
            errorTextArray.Add("Bit 5 - Error-Passive (EPI)");
        }
        else if ((errorCode & 0x08) != 0)
        {
            // MCP2515: RX1OVR or RX0OVR (Receive Buffer 0/1 Overflow)
            // SJA1000: DOI (Data Overrun Interrupt)
            errorTextArray.Add("Bit 3 - Data overrun (DOI)");
        }
        else if ((errorCode & 0x04) != 0)
        {
            // MCP2515: EWARN (Error Warning Flag)
            // SJA1000: EI (Error Warning Interrupt)
            errorTextArray.Add("Bit 2 - Error Warning (EI)");
        }
        else if ((errorCode & 0x02) != 0)
        {
            // MCP2515: not used
            // SJA1000: CAN transmit FIFO queue full
            errorTextArray.Add("Bit 1 - CAN transmit FIFO queue full");
        }
        else if ((errorCode & 0x01) != 0)
        {
            // MCP2515: not used
            // SJA1000: CAN receive FIFO queue full
            errorTextArray.Add("Bit 0 - CAN receive FIFO queue full");
        }

        // Write all error texts separated by | into the reading CAN_Fehler_Text
        errorTextString = string.Join("|", errorTextArray);
        _mqttService.SetReading("CAN_Error_Text", errorTextString);
        _mqttService.SetReading("CAN_Error", hexCodeString);
        _logger.LogDebug($"CAN Error: {errorTextString} ({errorCode})");
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
        _logger.LogInformation("UsbTinCanBusAdapter disposed.");
        // Beende den Service
    }
}
