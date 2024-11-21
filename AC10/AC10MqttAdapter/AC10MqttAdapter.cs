using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging;
using MQTTnet;
using MQTTnet.Client;
using MQTTnet.Protocol;
using MQTTnet.Diagnostics;
using MQTTnet.Adapter;
using MQTTnet.Implementations;
using System.Text;
using System.Collections.Concurrent;


namespace AC10Service;

public class AC10MqttAdapter: IDisposable
{
    private readonly    ILogger<AC10MqttAdapter>    _logger;
    private readonly    AC10MqttAdapterConfig       _config;
    private readonly    MqttFactory                 _mqttFactory;
    private             MqttClientOptions?          _mqttClientOptions;
    private             System.Timers.Timer         _openConnectionTimer;
    private             IMqttClient?                _mqttClient;
    private readonly    CancellationTokenSource     _cts = new CancellationTokenSource();
    private             ConcurrentQueue<(string ReadingName, string Value)>
                                                    _sendingQueue = new ConcurrentQueue<(string ReadingName, string Value)>();
    private             AutoResetEvent              _newSendingQueueElementEvent = new AutoResetEvent(false);

    public AC10MqttAdapter(IOptions<AC10MqttAdapterConfig> config, ILogger<AC10MqttAdapter> logger)
    {
        _config         = config.Value;
        _logger         = logger;
        _mqttFactory    = new MQTTnet.MqttFactory(new MqttNetLogger(_logger));
        ConfigureMqtt();

        _openConnectionTimer = new System.Timers.Timer(5000);
        _logger.LogInformation("AC10MqttAdapter initialized with configuration.");
    }

    private void ConfigureMqtt()
    {
        _mqttClientOptions = new MqttClientOptionsBuilder()
                                .WithConnectionUri(_config.ServerUri)
                                .WithClientId(_config.ClientId)
                                .WithoutThrowOnNonSuccessfulConnectResponse()
                                .Build();
        _mqttClient = _mqttFactory.CreateMqttClient();
    }
    
    public void Start()
    {
        _logger.LogInformation("Starting MQTT broker monitoring loop.");
        _openConnectionTimer.Elapsed += (sender, e) => OpenConnection();
        _openConnectionTimer.AutoReset    = true;
        _openConnectionTimer.Start();
        Task.Run(() => ProcessSendingQueue(_cts.Token), _cts.Token);
    }

    public void Stop()
    {
        _logger.LogInformation("Stop MQTT broker monitoring loop and terminate existing connections.");
        _cts.Cancel();
        _openConnectionTimer?.Stop();
        _openConnectionTimer?.Dispose();
        if( _mqttClient?.IsConnected == true)
        {
            try
            {
                _logger.LogInformation("Disconnecting from MQTT broker...");
                _mqttClient.DisconnectAsync().Wait();
                _logger.LogInformation("Disconnected from MQTT broker.");   
            }
            catch(Exception ex)
            {
                _logger.LogError(ex, "Disconnecting from MQTT broker failed.");
            }
        }
    }
    public void SendReading(string readingName, string value)
    {
        _logger.LogInformation($"Enqueuing MQTT message to topic '{_config.Topic}/{readingName}' with payload '{value}'...");
        // Initialize the queue if not already done
        _sendingQueue.Enqueue((readingName, value));
        _newSendingQueueElementEvent.Set();
        return;
    }

    private void ProcessSendingQueue(CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            if (_mqttClient?.IsConnected == true && _sendingQueue.TryDequeue(out var tuble) )
            { 
                PublishMessage(tuble.ReadingName, tuble.Value);
            } 
            else
            { 
                // Warte, bis ein neues Element in die Queue eingefügt wird oder die zeit abgelaufen ist
                _newSendingQueueElementEvent.WaitOne(TimeSpan.FromSeconds(1)); 
            } 
        }
    }

    private void PublishMessage(string topic, string payload)
    {
        try
        {
            _logger.LogInformation($"Sending MQTT message to topic '{_config.Topic}/{topic}' with payload '{payload}'...");
            var message = new MqttApplicationMessageBuilder()
                            .WithTopic(_config.Topic + "/" + topic)
                            .WithPayload(payload)
                            .WithQualityOfServiceLevel(MqttQualityOfServiceLevel.AtLeastOnce)
                            .Build();
            _mqttClient?.PublishAsync(message, _cts.Token).Wait();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Sending MQTT message to topic '{_config.Topic}/{topic}' with payload '{payload}' failed.");
        }
    }

    private void OpenConnection()
    {
        if(_mqttClientOptions == null || _mqttClient == null || _mqttClient.IsConnected==true)
        {
            return;
        }

        try
        {
            var connectResult = _mqttClient.ConnectAsync(_mqttClientOptions).GetAwaiter().GetResult();
            if (connectResult.ResultCode == MqttClientConnectResultCode.Success)
            {
                _logger.LogInformation($"Successfully connected to MQTT Broker {_mqttClientOptions.ChannelOptions.ToString()}.");
            }
            else
            {
                _logger.LogError($"Could not connection to MQTT Brocker {_mqttClientOptions.ChannelOptions.ToString()}. ResultCode: {connectResult.ResultCode}.");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Could not connection to MQTT Brocker{_mqttClientOptions.ChannelOptions.ToString()}. Trying again in 5 seconds.");
            return;
        }

    }

    public void Dispose()
    {
        _logger.LogInformation("AC10MqttAdapter disposed.");
        // Beende den Service
    }
}
