using System;
using System.Text;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Hosting.Systemd;
using Microsoft.Extensions.Logging;

namespace AC10Service;

public class AC10HeatingMqttService: IHostedService
{

    private readonly ILogger<AC10HeatingMqttService>    _logger;
    private readonly UsbTinCanBusAdapter                _usbTinCanBusAdapter;
    private readonly AC10MqttAdapter                    _ac10MqttAdapter;
    private readonly Dictionary<string, string>         _readings = new Dictionary<string, string>();
    private readonly CancellationTokenSource            _cts = new CancellationTokenSource();

    public AC10HeatingMqttService(ILogger<AC10HeatingMqttService> logger, UsbTinCanBusAdapter usbTinCanBusAdapter, AC10MqttAdapter ac10MqttAdapter)
    {
        _logger                 = logger;
        _usbTinCanBusAdapter    = usbTinCanBusAdapter;
        _ac10MqttAdapter        = ac10MqttAdapter;
        _logger.LogInformation("IsSystemd: {isSystemd}", SystemdHelpers.IsSystemdService());
    }
        
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Starting AC10HeatingMqttService...");
        _ac10MqttAdapter.Start();
        _usbTinCanBusAdapter.Start(SendReading);
        _ = Task.Run(() => ProcessConsoleInput(_cts.Token), _cts.Token);
        await Task.CompletedTask;
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Stopping AC10HeatingMqttService...");
        _cts.Cancel();
        _usbTinCanBusAdapter.Stop();
        _ac10MqttAdapter.Stop();
        // Your shutdown logic here
        await Task.CompletedTask;
    }

    private void ProcessConsoleInput(CancellationToken token)
    {
        _logger.LogInformation("Starte Keyboard Input Service...");
        while (!_cts.IsCancellationRequested) 
        { 
            if (Console.In.Peek() != -1)     
            {
                char keyChar = (char)Console.Read();

                ConsoleKey key = (ConsoleKey)keyChar;
                object? keyObject = null;
                if(Enum.TryParse(typeof(ConsoleKey), keyChar.ToString().ToUpper(),out keyObject)==true)
                {
                    key = (ConsoleKey)keyObject;
                }
                
                _logger.LogInformation("Input: {key}", key);
                if (key == ConsoleKey.R)
                { 
                    StringBuilder logMessage = new StringBuilder();
                    foreach (var item in _readings)
                    {
                        logMessage.AppendLine($"Reading: {item.Key} = {item.Value}");
                    }
                    logMessage.AppendLine($"Current Time: {DateTime.Now}");
                    _logger.LogInformation(logMessage.ToString());
                } 
                else if(key == ConsoleKey.C)
                {
                    _usbTinCanBusAdapter.SendLineWithoutResponse("C");
                }
                else if(key == ConsoleKey.O)
                {
                    _usbTinCanBusAdapter.SendLineWithoutResponse("O");
                }
                else if(key == ConsoleKey.N)
                {
                    _usbTinCanBusAdapter.SendLineWithoutResponse("");
                }                
                else if(key == ConsoleKey.F)
                {
                    _usbTinCanBusAdapter.SendLineWithoutResponse("F");
                }
                else if(key == ConsoleKey.V)
                {
                    _usbTinCanBusAdapter.SendLineWithoutResponse("V");
                }                                
                else if(key == ConsoleKey.D)
                {
                    _usbTinCanBusAdapter.Reset();
                }
                else if(key == ConsoleKey.S)
                {   //vond Stadard CanID (0x700) an den Mixer(0x601) senden  0x601, 0x0199(SOFTWARE_NUMMER)
                    _usbTinCanBusAdapter.RequestElsterValue(0xFFFF,0x601, 0x0199);
                    _usbTinCanBusAdapter.RequestElsterValue(0xFFFF,0x601, 0x0199);
                }
            } 
            Task.Delay(300); // Verhindert eine CPU-Überlastung 
        }
    }

    private void SendReading(string name, string value)
    {
        if (_readings.ContainsKey(name))
        {
            if (_readings[name] != value)
            {
                _readings[name] = value;
                SendMqttReading(name, value);
            }
        }
        else
        {
            _readings.Add(name, value);
            SendMqttReading(name, value);
        }
    }

    private void SendMqttReading(string name, string value)
    {
        _ac10MqttAdapter.SendReading(name, value);
    }

}

