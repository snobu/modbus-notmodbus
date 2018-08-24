# modbus-notmodbus

## Protocol translation from Modbus to MQTT/AMQP/HTTP for Azure IoT Hub

* MIT license
* .NET Core 2.1+
* Uses [ModbusTcp library](https://github.com/aviadmizrachi/ModbusTcp) from [Aviad Mizrachi](https://github.com/aviadmizrachi)
* Easy configuration through `appsettings.json`


### Configuration

Configuration is read from `appsettings.json` at runtime. All values should be decimal, not hex.


### Expected output
```
[DEBUG] Setting new pollingInterval: 66 seconds
[DEBUG] Serialized telemetry object:
{
  "iotHubDeviceId": "ModbusCollector",
  "voltage": [
    780,
    780
  ],
  "current": [
    780,
    780
  ]
}
[DEBUG] Sending message to Azure IoT Hub...
[DEBUG] Sent was successful at 7/16/2018 6:28:04 AM

Sleeping for 66 seconds /
```

Polling interval can be controlled either through `appsettings.json` or Device Twin &mdash;

![Device Twin screenshot](modbus-notmodbus/twin.png)

### Modbus simulator (Windows only):

* http://www.plcsimulator.org/downloads (MSI Windows Installer)
* Install the Vista Reg key from that link as well
* Click on the Scott Gu lookalike (red shirt) icon and click Walk to get random values away from zero
