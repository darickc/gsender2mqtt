using System;

namespace gsender2mqtt.Models;

public class Config
{
    public string ServerUrl { get; set; }
    public string GrblHalIpAddress { get; set; }
    public string MqttServer { get; set; }
    public int MqttPort { get; set; }
    public string MqttUsername { get; set; }
    public string MqttPassword { get; set; }
    public string MqttTopic { get; set; }
    public string MqttClientId { get; set; }
    public int RetryDelay { get; set; } = 60000;
    public bool IncludeAll { get; set; } = true;
    public bool IncludeStartup { get; set; }
    public bool IncludeSerialPort { get; set; }
    public bool IncludeController { get; set; }
    public bool IncludeFeeder { get; set; }
    public bool IncludeSender { get; set; }
    public bool IncludeWorkflow { get; set; }
    public bool IncludeConfig { get; set; }
    public bool IncludeError { get; set; }
    public bool IncludeFile { get; set; }
    public bool IncludeJob { get; set; }
    public bool IncludeHoming { get; set; }
    public bool IncludeGcodeError { get; set; }

}
