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
        if (_config.MqttServer == null || _config.MqttPort == 0 || _config.MqttUsername == null || _config.MqttPassword == null || _config.ServerUrl == null)
        {
            _logger.LogError("MQTT server, port, username, password, or server url is not set");
            
            return;
        }


        var mqttFactory = new MqttClientFactory();
        _mqttClient = mqttFactory.CreateMqttClient();
        var mqttClientOptions = new MqttClientOptionsBuilder()
                .WithTcpServer(_config.MqttServer, _config.MqttPort)
                .WithCredentials(_config.MqttUsername, _config.MqttPassword)
                .Build();


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
                await _mqttClient.PublishAsync(new MqttApplicationMessageBuilder()
                        .WithTopic($"{_config.MqttTopic}/status")
                        .WithPayload("disconnected")
                        .Build());
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
            (_config.IncludeWorkflow && e.StartsWith(WORKFLOW))))
            {
                await _mqttClient.PublishAsync(new MqttApplicationMessageBuilder()
                    .WithTopic($"{_config.MqttTopic}/{e}")
                    .WithPayload(data.ToString())
                    .Build());
            }
        });

        _client.On("serialport:list", async (data) =>
        {
            var serialPorts = JsonSerializer.Deserialize<List<List<SerialPort>>>(data.ToString(), _jsonSerializerOptions);
            var serialPort = serialPorts.SelectMany(p => p).FirstOrDefault(p => p.InUse);
            if (serialPort != null)
            {
                await _client.EmitAsync("open", serialPort.Port);
                await _mqttClient.PublishAsync(new MqttApplicationMessageBuilder()
                    .WithTopic($"{_config.MqttTopic}/status")
                    .WithPayload("connected")
                    .Build());
            }
            else
            {
                // no serial port found, disconnect from server and try again after a delay
                _logger.LogInformation("No active serial port found, disconnecting from server and trying again after a delay");
                await _client.DisconnectAsync();
            }
        });

        _client.OnError += (sender, e) =>
        {
            _logger.LogError($"Error: {e}");
        };

        while (!stoppingToken.IsCancellationRequested)
        {
            if (!_mqttClient.IsConnected)
                await _mqttClient.ConnectAsync(mqttClientOptions, stoppingToken);
            if (_mqttClient.IsConnected && !_client.Connected)
                await _client.ConnectAsync();
            await Task.Delay(_config.RetryDelay);
        }

        await _client.DisconnectAsync();
    }

    async ValueTask IAsyncDisposable.DisposeAsync()
    {
        if (_mqttClient.IsConnected)
        {
            await _mqttClient.PublishAsync(new MqttApplicationMessageBuilder()
                    .WithTopic($"{_config.MqttTopic}/status")
                    .WithPayload("disconnected")
                    .Build());
        }
        _client?.Dispose();
        _mqttClient?.Dispose();
        base.Dispose();
    }
}