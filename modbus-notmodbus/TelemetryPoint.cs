using System.Collections.Generic;

namespace modbus_notmodbus
{
    public class TelemetryPoint
    {
        public string iotHubDeviceId;
        public Dictionary<string, decimal> analogInput;
    }
}