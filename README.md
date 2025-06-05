# gsender2mqtt

A .NET application that bridges gsender events to MQTT, allowing you to integrate your gsender machine with MQTT-based home automation systems or other IoT applications.  Remote mode must be enabled on gsender.

## Features

- Connects to gsender via Socket.IO
- Publishes events to an MQTT broker
- Configurable event filtering
- Automatic reconnection handling
- Docker support
- Environment-based configuration

## Supported Events

The application can publish the following types of events to MQTT:

- Status events (connection state)
- Startup events
- Serial port events
- Controller events
- Feeder events
- Sender events
- Workflow events
- Configuration events
- Error events
- File events
- Job events
- Homing events
- G-code error events

## Configuration

The application can be configured using environment variables:

| Variable | Description | Default |
|----------|-------------|---------|
| `SERVERURL` | URL of the gsender server | `http://localhost:8000` |
| `MQTTSERVER` | MQTT broker hostname | `localhost` |
| `MQTTPORT` | MQTT broker port | `1883` |
| `MQTTUSERNAME` | MQTT username | - |
| `MQTTPASSWORD` | MQTT password | - |
| `MQTTTOPIC` | Base MQTT topic | `gsender` |
| `MQTTCLIENTID` | MQTT client ID | `gsender2mqtt` |
| `RETRYDELAY` | Delay between reconnection attempts (ms) | `60000` |
| `INCLUDEALL` | Include all events | `true` |
| `INCLUDESTARTUP` | Include startup events | `false` |
| `INCLUDESERIALPORT` | Include serial port events | `false` |
| `INCLUDECONTROLLER` | Include controller events | `false` |
| `INCLUDEFEEDER` | Include feeder events | `false` |
| `INCLUDESENDER` | Include sender events | `false` |
| `INCLUDEWORKFLOW` | Include workflow events | `false` |
| `INCLUDECONFIG` | Include configuration events | `false` |
| `INCLUDEERROR` | Include error events | `false` |
| `INCLUDEFILE` | Include file events | `false` |
| `INCLUDEJOB` | Include job events | `false` |
| `INCLUDEHOMING` | Include homing events | `false` |
| `INCLUDEGCODEERROR` | Include G-code error events | `false` |

## Running with Docker

1. Build the Docker image:
```bash
docker build -t gsender2mqtt .
```

2. Run the container:
```bash
docker run -d \
  -e SERVERURL=http://your-gsender-server:8000 \
  -e MQTTSERVER=your-mqtt-broker \
  -e MQTTPORT=1883 \
  -e MQTTUSERNAME=your-username \
  -e MQTTPASSWORD=your-password \
  -e MQTTTOPIC=gsender \
  -e MQTTCLIENTID=gsender2mqtt \
  gsender2mqtt
```

## Running Locally

1. Install .NET 9.0 SDK
2. Clone the repository
3. Build the project:
```bash
dotnet build
```
4. Run the application:
```bash
dotnet run
```

## MQTT Topics

Events are published to MQTT topics in the following format:
```
{MQTTTOPIC}/{event_type}
```

For example:
- `gsender/status` - Connection status
- `gsender/startup` - Startup events
- `gsender/serialport` - Serial port events
- `gsender/controller` - Controller events
- `gsender/feeder` - Feeder events
- `gsender/sender` - Sender events
- `gsender/workflow` - Workflow events

## Example Data

The project includes example data files in the `ExampleData` directory that demonstrate the format of various events. These examples can be useful for testing and understanding the event structure:

- `status` - Shows connection status events (e.g., "disconnected")
- `startup` - Contains startup configuration including loaded controllers and available ports
- `file.load` - Example of file loading events
- `job:start` - Example of job start events
- `gcode_error` - Example of G-code error events
- `homing:flag` - Example of homing events
- `config:change` - Example of configuration change events
- `grbleHal:info` - Example of controller information events
- `sender:status` - Example of sender status events

These example files can be used to test your MQTT integration or to understand the expected format of different event types.

## License

MIT
