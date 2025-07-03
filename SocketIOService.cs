using System;
using System.Text.Json;
using gsender2mqtt.Models;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MQTTnet;
using SocketIO.Serializer.SystemTextJson;
using SocketIOClient;

namespace gsender2mqtt;

public class SocketIOService : BackgroundService, IAsyncDisposable
{
    private readonly ILogger<SocketIOService> _logger;
    private SocketIOClient.SocketIO _client;
    private IMqttClient _mqttClient;
    private JsonSerializerOptions _jsonSerializerOptions;
    private Config _config;

    private const string STATUS = "status";
    private const string STARTUP = "startup";
    private const string SERIALPORT = "serialport";
    private const string CONTROLLER = "controller";
    private const string FEEDER = "feeder";
    private const string SENDER = "sender";
    private const string WORKFLOW = "workflow";
    private const string CONFIG = "config";
    private const string ERROR = "error";
    private const string FILE = "file";
    private const string JOB = "job";
    private const string HOMING = "homing";
    private const string GCODE_ERROR = "gcode_error";
    private const string EMIT = "emit";


    public SocketIOService(ILogger<SocketIOService> logger, Config config)
    {
        _logger = logger;
        _jsonSerializerOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };
        _config = config;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (_config.MqttServer == null || _config.MqttPort == 0 || _config.ServerUrl == null)
        {
            _logger.LogError("MQTT server, port, or server url is not set");

            return;
        }


        var mqttFactory = new MqttClientFactory();
        _mqttClient = mqttFactory.CreateMqttClient();
        var mqttClientOptions = new MqttClientOptionsBuilder()
                .WithTcpServer(_config.MqttServer, _config.MqttPort)
                .WithCredentials(_config.MqttUsername, _config.MqttPassword)
                .Build();
        if (!string.IsNullOrWhiteSpace(_config.MqttEmitTopic))
        {
            _mqttClient.ApplicationMessageReceivedAsync += async (e) =>
            {
                _logger.LogInformation($"MQTT received: {e.ApplicationMessage.Topic}");
                if (_client?.Connected ?? false && e.ApplicationMessage.Topic.StartsWith($"{_config.MqttEmitTopic}/{EMIT}"))
                {
                    try
                    {
                        var eventName = e.ApplicationMessage.Topic.Replace($"{_config.MqttTopic}/{EMIT}/", "");
                        var payload = e.ApplicationMessage.ConvertPayloadToString();
                        var payloadObject = JsonSerializer.Deserialize<object>(payload, _jsonSerializerOptions);
                        _logger.LogInformation($"Emitting event: {eventName} with payload: {payload}");
                        if (!string.IsNullOrWhiteSpace(payload))
                        {
                            // this is not working, at least with what I have tried so far.
                            await _client.EmitAsync(eventName, payloadObject);
                        }
                        else
                        {
                            await _client.EmitAsync(eventName);
                        }
                    }
                    catch (ObjectDisposedException)
                    {
                        _logger.LogWarning("SocketIO client was disposed while trying to emit message");
                    }
                }
            };
        }

        _client = new SocketIOClient.SocketIO(_config.ServerUrl, new SocketIOOptions
        {
            Transport = SocketIOClient.Transport.TransportProtocol.WebSocket,
            Path = "/socket.io"
        });
        _client.Serializer = new SystemTextJsonSerializer(_jsonSerializerOptions);

        _client.OnConnected += async (sender, e) =>
        {
            _logger.LogInformation("Connected to server");
            await _client.EmitAsync("list");
        };

        _client.OnDisconnected += async (sender, e) =>
        {
            _logger.LogInformation("Disconnected from server");
            if (_mqttClient.IsConnected)
            {
                await PublishAsync(STATUS, "disconnected");
            }
        };

        _client.OnAny(async (e, data) =>
        {
            _logger.LogInformation($"Event: {e}, Data: {data}");
            if (_mqttClient.IsConnected && (_config.IncludeAll ||
            (_config.IncludeStartup && e.StartsWith(STARTUP)) ||
            (_config.IncludeSerialPort && e.StartsWith(SERIALPORT)) ||
            (_config.IncludeController && e.StartsWith(CONTROLLER)) ||
            (_config.IncludeFeeder && e.StartsWith(FEEDER)) ||
            (_config.IncludeSender && e.StartsWith(SENDER)) ||
            (_config.IncludeWorkflow && e.StartsWith(WORKFLOW)) ||
            (_config.IncludeConfig && e.StartsWith(CONFIG)) ||
            (_config.IncludeError && e.StartsWith(ERROR)) ||
            (_config.IncludeFile && e.StartsWith(FILE)) ||
            (_config.IncludeJob && e.StartsWith(JOB)) ||
            (_config.IncludeHoming && e.StartsWith(HOMING)) ||
            (_config.IncludeGcodeError && e.StartsWith(GCODE_ERROR))))
            {
                await PublishAsync(e, data.ToString());
            }
        });

        _client.On("serialport:list", async (data) =>
        {
            var serialPorts = JsonSerializer.Deserialize<List<List<SerialPort>>>(data.ToString(), _jsonSerializerOptions);
            var serialPort = serialPorts.SelectMany(p => p).FirstOrDefault(p => p.InUse);
            if (serialPort != null)
            {
                await _client.EmitAsync("open", serialPort.Port);
            }
            else if (_config.GrblHalIpAddress != null)
            {
                await _client.EmitAsync("open", _config.GrblHalIpAddress);
            }
            else
            {
                // no serial port found, disconnect from server and try again after a delay
                _logger.LogInformation("No active serial port found, disconnecting from server and trying again after a delay");
                await _client.DisconnectAsync();
            }
        });

        _client.On("serialport:open", async (data) =>
        {
            await PublishAsync(STATUS, "connected");
        });

        _client.OnError += (sender, e) =>
        {
            _logger.LogError($"Error: {e}");
        };

        while (!stoppingToken.IsCancellationRequested)
        {
            if (!_mqttClient.IsConnected)
            {
                await _mqttClient.ConnectAsync(mqttClientOptions, stoppingToken);
                await _mqttClient.SubscribeAsync(new MqttClientSubscribeOptionsBuilder()
                    .WithTopicFilter($"{_config.MqttTopic}/{EMIT}/#")
                    .Build(), stoppingToken);
            }
            if (_mqttClient.IsConnected && !_client.Connected)
                await _client.ConnectAsync();
            await Task.Delay(_config.RetryDelay);
        }

        await _client.DisconnectAsync();
    }

    private async Task PublishAsync(string topic, string payload)
    {
        await _mqttClient.PublishAsync(new MqttApplicationMessageBuilder()
                .WithTopic($"{_config.MqttTopic}/{topic}")
                .WithPayload(payload)
                .WithRetainFlag(true)
                .WithQualityOfServiceLevel(MQTTnet.Protocol.MqttQualityOfServiceLevel.AtLeastOnce)
                .Build());
    }

    async ValueTask IAsyncDisposable.DisposeAsync()
    {
        if (_mqttClient?.IsConnected ?? false)
        {
            await PublishAsync(STATUS, "disconnected");
        }
        _client?.Dispose();
        _mqttClient?.Dispose();
        base.Dispose();
    }
}