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
        var canScanValue = _configuration.GetValue<string>("can_scan");
        if (canScanValue != null)
        {
            ExecuteCanScan(canScanValue);
        }

        var msgScanValue = _configuration.GetValue<string>("msg_scan");
        if (msgScanValue != null)
        {
            ExecuteMsgScan(msgScanValue);
        }

        var moduleScanValue = _configuration.GetValue<string>("modules_scan");
        if (moduleScanValue != null)
        {
            ExecuteModulesScan(moduleScanValue);
        }

        if (canScanValue != null || moduleScanValue != null || msgScanValue != null)
        {
            _logger.LogInformation("The program will be stopped after execution of programm parameters like can_scan...");
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
        if(!WaitUntilCanBusIsOpen())
        {
            _logger.LogError("CanBus is not open. Can't execute can_scan");
            return;
        }
    }

    private void ExecuteMsgScan(string msg_scan_value)
    {
        _logger.LogInformation($"Evaluate parameter msg_scan '{msg_scan_value}'...");
        // --==if Systemd Service is running, then skip can_scan==--       
        if(SystemdHelpers.IsSystemdService()==true)
        {
            _logger.LogInformation("In Systemd Service, skipping parameter module_scan");
            return;
        }
      
        // --==Check any parameters and cast them to type safe values==--    
        //  timespan: optional, collection time span in ISO 8601 format (e.g. PT10h)");
        if(string.IsNullOrEmpty(msg_scan_value))
        {
            msg_scan_value = "PT10h";
        }
        if(ElsterUserInputParser.ParseISO8601Timespan(msg_scan_value,out TimeSpan? duration)==false)
        {
            LogMsgScanSyntax("time has invalid format");
            return;
        }

        // --==Wait until CanBus is open, or can_scan is skipped==--
        if(!WaitUntilCanBusIsOpen())
        {
            _logger.LogError("CanBus is not open. Can't execute can_scan");
            return;
        }
        // --==Start collecting messages and stop and log ==--
        _heatingAdapter.Value.PrintPassiveElsterTelegramList();
        Thread.Sleep(duration ?? TimeSpan.FromHours(10));
        _heatingAdapter.Value.PrintPassiveElsterTelegramList();
 
    }


    private void ExecuteModulesScan(string modules_scan_value)
    {
        _logger.LogInformation($"Evaluate parameter modules_scan '{modules_scan_value}'...");
        // --==if Systemd Service is running, then skip can_scan==--       
        if(SystemdHelpers.IsSystemdService()==true)
        {
            _logger.LogInformation("In Systemd Service, skipping parameter module_scan");
            return;
        }
      
        // --==Check any parameters and cast them to type safe values==--    
        ElsterModule    senderCanID     = ElsterUserInputParser.ParseSenderElsterModule(modules_scan_value);
        if((ushort)senderCanID==0xFFF && modules_scan_value.ToLower()!="default")
        {
            LogModuleScanSyntax("SenderCanID has invalid format");
            return;
        }

        // --==Wait until CanBus is open, or can_scan is skipped==--
        if(!WaitUntilCanBusIsOpen())
        {
            _logger.LogError("CanBus is not open. Can't execute can_scan");
            return;
        }

        _heatingAdapter.Value.ScanElsterModules((ushort)senderCanID);
    }

    private bool WaitUntilCanBusIsOpen(int sec = 30)
    {
        _logger.LogInformation($"Waiting up to {sec} seconds for Mqtt-Broker and CanBus to become available...");
        int counter = 0;
        while (!_usbTinCanBusAdapter.Value.IsCanBusOpen && counter < sec)
        {
            Thread.Sleep(1000);
            counter++;
        }
        return _usbTinCanBusAdapter.Value.IsCanBusOpen;
    }


    private void LogMsgScanSyntax(string? specificErrorMessage)
    {
        if(specificErrorMessage != null) _logger.LogError($"Wrong syntax in parameter msg_scan:{specificErrorMessage}");
        _logger.LogError("");
        _logger.LogError("Syntax:");
        _logger.LogError("HeatingMqttService --msg_scan=[timespan]");
        _logger.LogError("");
        _logger.LogError("   timespan: optional, collection time span in ISO 8601 format (e.g. PT10h)");
        _logger.LogError("");
        _logger.LogError("Example: HeatingMqttService --msg_scan=PT10h        (collect all telegrams with an elster value for 10 hours)");
        _logger.LogError("OR       HeatingMqttService --msg_scan=             (collect all telegrams with an elster value for 10 hours)");       
    }
    
    private void LogModuleScanSyntax(string? specificErrorMessage)
    {
        if(specificErrorMessage != null) _logger.LogError($"Wrong syntax in parameter module_scan:{specificErrorMessage}");
        _logger.LogError("");
        _logger.LogError("Syntax:");
        _logger.LogError("HeatingMqttService --module_scan=[SenderCanID]");
        _logger.LogError("");
        _logger.LogError("   SenderCanID: optional, default is standard CanId from appsettings.json. Hex-Value or module name (e.g. 700 or ExternalDevice)");
        _logger.LogError("");
        _logger.LogError("Example: HeatingMqttService --module_scan=default         (scan all modules with default sender can id)");
        _logger.LogError("OR       HeatingMqttService --module_scan=700             (use 700 as sender can id to scan all modules)");
        _logger.LogError("OR       HeatingMqttService --module_scan=ExternalDevice  (use 700 as sender can id to scan all modules)");
    }

    private void LogCanSendSyntax(string? specificErrorMessage)
    {
        if(specificErrorMessage != null) _logger.LogError($"Wrong syntax in parameter can_scan:{specificErrorMessage}");
        _logger.LogError("");
        _logger.LogError("Syntax:");
        _logger.LogError("HeatingMqttService --can_scan=[SenderCanID] ReceiverCanID[.ElsterIndex[.NewElsterValue]]");
        _logger.LogError("");
        _logger.LogError("   SenderCanID: optional, default is standard CanId from appsettings.json. Hex-Value or module name (e.g. 700 or ExternalDevice)");
        _logger.LogError("   ReceiverCanID: mandatory, hex-Value or module name (e.g. 301 or RemoteControl)");
        _logger.LogError("   ElsterIndex: optional to read or write a single value. Hex-Value or elster index name (e.g. 000b or GERAETE_ID)");
        _logger.LogError("   NewElsterValue: optional to write a single value. Hex-Value (e.g. 0f00)");
        _logger.LogError("");
        _logger.LogError("Example: HeatingMqttService --can_scan=180               (scan all elster indices from 0000 to 1fff)");
        _logger.LogError("OR       HeatingMqttService --can_scan=700 180           (use 700 as sender can id to scan all elster indices");
        _logger.LogError("OR       HeatingMqttService --can_scan=700 180.0126      (read minutes at elster index 0126)");
        _logger.LogError("OR       HeatingMqttService --can_scan=700 180.0126.0f00 (set minutes to 15)");
        _logger.LogError("OR       HeatingMqttService --can_scan=700 Boiler.MINUTE (read minutes at elster index 0126)");
    }
}

