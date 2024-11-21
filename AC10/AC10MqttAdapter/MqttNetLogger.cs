using MQTTnet.Diagnostics;
using MQTTnet.Client;
using Microsoft.Extensions.Logging;
using System;

namespace AC10Service;

public class MqttNetLogger : IMqttNetLogger
{
    private readonly ILogger<AC10MqttAdapter> _logger;

    public MqttNetLogger(ILogger<AC10MqttAdapter> logger)
    {
        _logger = logger;
    }

    bool IMqttNetLogger.IsEnabled { get => true; }

    public void Publish(MqttNetLogLevel logLevel, string source, string message, object[] args, Exception exception)
    {
        var logMessage = $"{source}: {message}";
        switch (logLevel)
        {
            case MqttNetLogLevel.Error:
                _logger.LogError(exception, logMessage, args);
                break;
            case MqttNetLogLevel.Warning:
                _logger.LogWarning(exception, logMessage, args);
                break;
            case MqttNetLogLevel.Info:
                _logger.LogInformation(exception, logMessage, args);
                break;
            case MqttNetLogLevel.Verbose:
                _logger.LogTrace(exception, logMessage, args);
                break;
            default:
                _logger.LogInformation(exception, logMessage, args);
                break;
        }
    }
}
