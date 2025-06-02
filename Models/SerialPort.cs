using System;

namespace gsender2mqtt.Models;

public class SerialPort
{
    public string Port { get; set; }
    public string Manufacturer { get; set; }
    public bool InUse { get; set; }
}
