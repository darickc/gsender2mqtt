using System;
using System.Text.Json;
using gsender2mqtt.Models;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MQTTnet;
using SocketIO.Serializer.SystemTextJson;
using SocketIOClient;

namespace gsender2mqtt;

public class SocketIOService : BackgroundService
{
    private readonly ILogger<SocketIOService> _logger;
    private SocketIOClient.SocketIO _client;
    private IMqttClient _mqttClient;
    private JsonSerializerOptions _jsonSerializerOptions;
    private Config _config;

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

        _client.OnDisconnected += (sender, e) =>
        {
            _logger.LogInformation("Disconnected from server");
        };

        _client.OnAny((e, data) =>
        {
            _logger.LogInformation($"Event: {e}, Data: {data}");
            if(_mqttClient.IsConnected)
            {
                _mqttClient.PublishAsync(new MqttApplicationMessageBuilder()
                    .WithTopic($"{_config.MqttTopic}/{e}")
                    .WithPayload(data.ToString())
                    .Build());
            }
        });

        _client.On("serialport:list", (data) =>
        {
            _logger.LogInformation($"serialport:list: {data}");
            
            var serialPorts = JsonSerializer.Deserialize<List<List<SerialPort>>>(data.ToString(), _jsonSerializerOptions);
            var serialPort = serialPorts.SelectMany(p => p).FirstOrDefault(p => p.InUse);
            if (serialPort != null)
            {
                _client.EmitAsync("open", serialPort.Port);
            }
        });

        _client.OnError += (sender, e) =>
        {
            _logger.LogError($"Error: {e}");
        };

        while(!stoppingToken.IsCancellationRequested)
        {
            if(!_mqttClient.IsConnected)
                await _mqttClient.ConnectAsync(mqttClientOptions, stoppingToken);  
            if(_mqttClient.IsConnected && !_client.Connected)
                await _client.ConnectAsync();
            await Task.Delay(1000);
        }

        await _client.DisconnectAsync();
    }

    

    public override void Dispose()
    {
        _client?.Dispose();
        _mqttClient?.Dispose();
        base.Dispose();
    }
} 