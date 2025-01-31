using System;
using System.Text;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Hosting.Systemd;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging;

namespace HeatingDaemon;

public class HeatingMqttService: IHostedService
{

    private readonly ILogger<HeatingMqttService>    _logger;
    private readonly HeatingMqttServiceConfig       _heatingMqttServiceConfig;
    private readonly Lazy<UsbTinCanBusAdapter>      _usbTinCanBusAdapter;
    private readonly Lazy<MqttAdapter>              _ac10MqttAdapter;
    private readonly Lazy<HeatingAdapter>           _heatingAdapter;
    private readonly CancellationTokenSource        _cts = new CancellationTokenSource();

    public HeatingMqttService(  IOptions<HeatingMqttServiceConfig> heatingMqttServiceConfig,
                                Lazy<UsbTinCanBusAdapter> usbTinCanBusAdapter,
                                Lazy<MqttAdapter> ac10MqttAdapter,
                                Lazy<HeatingAdapter> heatingAdapter,
                                ILogger<HeatingMqttService> logger
                                )
    {
        _logger                     = logger;
        _heatingMqttServiceConfig   = heatingMqttServiceConfig.Value;
        _usbTinCanBusAdapter        = usbTinCanBusAdapter;
        _ac10MqttAdapter            = ac10MqttAdapter;
        _heatingAdapter             = heatingAdapter;
        _logger.LogInformation("IsSystemd: {isSystemd}", SystemdHelpers.IsSystemdService());
    }
        
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Starting HeatingMqttService...");
        _ac10MqttAdapter.Value.Start();
        _usbTinCanBusAdapter.Value.Start();
        _ = Task.Run(() => ProcessConsoleInput(_cts.Token), _cts.Token);
        _ = Task.Run(() => ProcessCyclicReadingsQuery(_cts.Token), _cts.Token);
        await Task.CompletedTask;
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Stopping HeatingMqttService...");
        _cts.Cancel();
        _usbTinCanBusAdapter.Value.Stop();
        _ac10MqttAdapter.Value.Stop();
        // Your shutdown logic here
        await Task.CompletedTask;
    }

    private void ProcessCyclicReadingsQuery(CancellationToken token)
    {
        _logger.LogInformation("Starte CyclicReadingsQuery Service...");
        if(_heatingMqttServiceConfig.CyclicReadingsQuery == null)
        {
            _logger.LogWarning("No CyclicReadingsQuery configuration found. CyclicReadingsQuery is null");
             return;
        }

        // Read all CyclicReadingsQuery configurations
        List<CyclicReadingQueryDto> cyclicReadingQueryList = new List<CyclicReadingQueryDto>();
        _heatingMqttServiceConfig.CyclicReadingsQuery.ForEach(queryConfig =>
        {
             CyclicReadingQueryDto? item = queryConfig.ToCyclicReadingQueryDto();
             if(item != null)
             {
                 cyclicReadingQueryList.Add(item);
             }
             else
             {
                 _logger.LogWarning($"CyclicReadingsQuery configuration is invalid: {queryConfig}");
             }
            _logger.LogInformation($"CyclicReadingsQuery: {queryConfig}");
        });
       
        // Start the cyclic reading query loop
        _heatingAdapter.Value.CyclicReadingLoop(token, cyclicReadingQueryList);
        _logger.LogInformation("Stopping CyclicReadingsQuery Service...");
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
                    _ac10MqttAdapter.Value.LogAllReadings();
                } 
                else if(key == ConsoleKey.P)
                {
                    _heatingAdapter.Value.PrintPassiveElsterTelegramList();
                }
                else if(key == ConsoleKey.C)
                {
                    _usbTinCanBusAdapter.Value.SendLineWithoutResponse("C");
                }
                else if(key == ConsoleKey.O)
                {
                    _usbTinCanBusAdapter.Value.SendLineWithoutResponse("O");
                }
                else if(key == ConsoleKey.N)
                {
                    _usbTinCanBusAdapter.Value.SendLineWithoutResponse("");
                }                
                else if(key == ConsoleKey.F)
                {
                    _usbTinCanBusAdapter.Value.SendLineWithoutResponse("F");
                }
                else if(key == ConsoleKey.V)
                {
                    _usbTinCanBusAdapter.Value.SendLineWithoutResponse("V");
                }                                
                else if(key == ConsoleKey.D)
                {
                    _usbTinCanBusAdapter.Value.Reset();
                }
                else if(key == ConsoleKey.S)
                {   // Tages Ertrag in kwh abfragen an HeatingModule 
                    ElsterValue? elsterValue;
                    _heatingAdapter.Value.RequestElsterValue(0xFFFF, 0x0500, 0x092f, out elsterValue);
                    //von Stadard CanID (0x700) an den Mixer(0x601) senden  0x601, 0x0199(SOFTWARE_NUMMER)
                    //_heatingAdapter.Value.RequestElsterValue(0xFFFF,0x601, 0x019a, out elsterValue);
                }
                else if(key == ConsoleKey.M)
                { 
                    _heatingAdapter.Value.ScanElsterModules();
                }
            } 
            Thread.Sleep(300); // Verhindert eine CPU-Überlastung 
        }
        _logger.LogInformation("Stopping Keyboard Input Service...");
    }
}

