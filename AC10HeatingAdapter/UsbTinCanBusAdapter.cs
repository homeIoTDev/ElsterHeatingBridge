using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging;
using System.IO.Ports;
using System.Text;

namespace AC10Service;
// soll UsbTinCanBusAdapter
public class UsbTinCanBusAdapter: IDisposable
{
    private readonly ILogger<UsbTinCanBusAdapter>   _logger;
    private readonly UsbTinCanBusAdapterConfig      _config;
    private SerialPort                              _serialPort;
    private System.Timers.Timer                     _openPortTimer;
    private readonly AC10HeatingAdapter             _ac10HeatingAdapter;
    private Action<string,string>?                  _sendReadingCallback;



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
                _ac10HeatingAdapter.Start(SendLineCallback, _sendReadingCallback);
            }
            else
            {    
                _logger.LogError($"Could not start USBtinCanBusAdapter because _sendReadingCallback is null.");
            }
        }
    }

    private void SendLineCallback(string line)
    {
        if (_serialPort.IsOpen)
        {
            _logger.LogDebug("Tx serial port: '{line}'", line);
            _serialPort.WriteLine(line);
        }
        else
        {
            _logger.LogError($"Could not send line '{line}' because serial port {_config.PortName} is not open.");
        }
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
                        break;
                    }
                    else if( c == 7)    //beel 
                    {
                            lineBuilder.Append(c);    // when <bell> found add to line, to be able to process errors in the main loop
                        break;
                    }
                    lineBuilder.Append(c);
                }
                string line = lineBuilder.ToString();
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

    public void Dispose()
    {
        _logger.LogInformation("AC10HeatingAdapter disposed.");
        // Beende den Service
    }
}
