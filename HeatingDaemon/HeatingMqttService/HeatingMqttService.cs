using System;
using System.Text;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Hosting.Systemd;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using HeatingDaemon.ElsterProtocol;

namespace HeatingDaemon;

public class HeatingMqttService: IHostedService
{

    private readonly ILogger<HeatingMqttService>    _logger;
    private readonly HeatingMqttServiceConfig       _heatingMqttServiceConfig;
    private readonly Lazy<UsbTinCanBusAdapter>      _usbTinCanBusAdapter;
    private readonly Lazy<MqttAdapter>              _ac10MqttAdapter;
    private readonly Lazy<HeatingAdapter>           _heatingAdapter;
    private readonly CancellationTokenSource        _cts = new CancellationTokenSource();
    private          IConfiguration                 _configuration;
    private          IHostApplicationLifetime       _applicationLifetime;

    public HeatingMqttService(  IOptions<HeatingMqttServiceConfig> heatingMqttServiceConfig,
                                Lazy<UsbTinCanBusAdapter> usbTinCanBusAdapter,
                                Lazy<MqttAdapter> ac10MqttAdapter,
                                Lazy<HeatingAdapter> heatingAdapter,
                                ILogger<HeatingMqttService> logger,
                                IConfiguration configuration,
                                IHostApplicationLifetime applicationLifetime
                            )
    {
        _logger                     = logger;
        _heatingMqttServiceConfig   = heatingMqttServiceConfig.Value;
        _usbTinCanBusAdapter        = usbTinCanBusAdapter;
        _ac10MqttAdapter            = ac10MqttAdapter;
        _heatingAdapter             = heatingAdapter;
        _configuration              = configuration;
        _applicationLifetime        = applicationLifetime;
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
        var can_scanValue = _configuration.GetValue<string>("can_scan");// where the argument is --can_scan=301.000b
        if(can_scanValue != null)
        {
            ExecuteCanScan(can_scanValue);
            _logger.LogInformation("Programm wird beendet...");
            _cts.Cancel();
            _applicationLifetime.StopApplication();
        }

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

    /// <summary>
    /// Evaluate the parameter can_scan, with the following structure: [680] 301[.000b.[4567]]
    /// If Systemd Service is running, then skip can_scan
    /// </summary>
    /// <param name="can_scanValue">The value of the can_scan parameter</param>
    private void ExecuteCanScan(string can_scanValue)
    {
        _logger.LogInformation($"Evaluate parameter can_scan '{can_scanValue}'...");
        // --==if Systemd Service is running, then skip can_scan==--       
        if(SystemdHelpers.IsSystemdService()==true)
        {
            _logger.LogInformation("In Systemd Service, skipping parameter can_scan");
            return;
        }
        // --==parse the can_scan parameter, with the following structure: [680] 301[.000b.[4567]]==--       
        string[] can_scanParts = can_scanValue.Trim().Split(' ');
        if(can_scanParts.Length<1||can_scanParts.Length>2)
        {
            LogCanSendSyntax("The value part of parameter can_scan is missing or invalid");
            return;
        }
        //überprüfen, ob ein Sender-CAN-ID angegeben wurde
        string? senderCanIdStr = null;
        string elsterPartStr = can_scanParts[0];
        if(can_scanParts.Length==2)
        {
            senderCanIdStr=can_scanParts[0];
            elsterPartStr = can_scanParts[1];
        }
        // der elsterModuleStr kann nun noch aus zwei optionalen Teilen bestehen
        string[] elsterModuleArray = elsterPartStr.Split('.');
        if(elsterModuleArray.Length==0 || string.IsNullOrEmpty(elsterModuleArray[0]))
        {
            LogCanSendSyntax("The elster receiver part part of parameter can_scan is missing or invalid");
            return;
        }
        string elsterModuleStr = elsterModuleArray[0];
        string? elsterIndexStr = (elsterModuleArray.Length>1) ? elsterModuleArray[1] : null;
        string? elsterValueStr = (elsterModuleArray.Length>2) ? elsterModuleArray[2] : null;

        // --==Check any parameters and cast them to type safe values==--    
        //senderCanIdStr > ElsterModule == take default SenderCanId 0xFFF
        //elsterModuleStr > ElsterModule 
        //elsterIndexStr > ushort? == scann all indexes on module
        //elsterValueStr > ushort? == write an single value
        ElsterModule    senderCanID     = ElsterUserInputParser.ParseSenderElsterModule(senderCanIdStr);
        ElsterModule?   receiverCanID   = ElsterUserInputParser.ParseReceiverElsterModule(elsterModuleStr);
        ushort?         elsterIndex     = ElsterUserInputParser.ParseElsterIndex(elsterIndexStr);
        ushort?         elsterValue     = ElsterUserInputParser.ParseElsterValue(elsterValueStr);
        if(receiverCanID==null)
        {
            LogCanSendSyntax("ReceiverCanID has invalid format");
            return;
        }

        // --==Wait until CanBus is open, or can_scan is skipped==--
        int counter = 0;
        while (!_usbTinCanBusAdapter.Value.IsCanBusOpen && counter < 10)
        {
            Thread.Sleep(1000);
            counter++;
        }
        if (!_usbTinCanBusAdapter.Value.IsCanBusOpen)
        {
            _logger.LogInformation("CanBus not open after 10 seconds, skipping can_scan");
            return;
        }
    }

    private void LogCanSendSyntax(string specificErrorMessage)
    {
        StringBuilder logMessage = new StringBuilder();
        logMessage.Append("Wrong syntax in parameter can_scan:");
        logMessage.AppendLine(specificErrorMessage);
        logMessage.AppendLine("");
        logMessage.AppendLine("Syntax:");
        logMessage.AppendLine("HeatingMqttService --can_scan=[SenderCanID] ReceiverCanID[.ElsterIndex[.NewElsterValue]]");
        logMessage.AppendLine("");
        logMessage.AppendLine("   SenderCanID: optional, default is standard CanId from appconfig. Hex-Value or module name (e.g. 700 or ExternalDevice)");
        logMessage.AppendLine("   ReceiverCanID: mandatory, hex-Value or module name (e.g. 301 or RemoteControl)");
        logMessage.AppendLine("   ElsterIndex: optional to read or write a single value. Hex-Value or elster index name (e.g. 000b or GERAETE_ID)");
        logMessage.AppendLine("   NewElsterValue: optional to write a single value. Hex-Value (e.g. 0f00)");
        logMessage.AppendLine("");
        logMessage.AppendLine("Example: HeatingMqttService --can_scan=180               (scan all elster indices from 0000 to 1fff)");
        logMessage.AppendLine("OR       HeatingMqttService --can_scan=700 180           (use 700 as sender can id to scan all elster indices");
        logMessage.AppendLine("OR       HeatingMqttService --can_scan=700 180.0126      (read minutes at elster index 0126)");
        logMessage.AppendLine("OR       HeatingMqttService --can_scan=700 180.0126.0f00 (set minutes to 15)");
        logMessage.AppendLine("OR       HeatingMqttService --can_scan=700 Boiler.MINUTE (read minutes at elster index 0126)");

        _logger.LogError(logMessage.ToString());
    }
}

