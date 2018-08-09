using System;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.Devices.Client;
using Microsoft.Azure.Devices.Shared;
using Microsoft.Extensions.Configuration;
using ModbusTcp;
using Newtonsoft.Json;

namespace modbus_notmodbus
{
    public class ModbusAdvocate
    {
        internal ModbusClient modbusClient;
        internal DeviceClient deviceClient;
        public static uint PollingInterval = AppSettings.pollingInterval;
        internal bool modbusClientAlive = false;

        public DeviceClient Client => deviceClient;

        public async Task InitAsync(
            string host,
            int port,
            string iotHubDeviceConnStr,
            DesiredPropertyUpdateCallback desiredPropertyUpdateCallback)
        {
            modbusClient = new ModbusClient(host, port);
            try
            {
                modbusClient.Init();
                modbusClientAlive = true;
            }
            catch (Exception ex)
            {
                Misc.LogException($"Exception while instantiating Modbus client: {ex.Message}");
            }

            deviceClient = DeviceClient.CreateFromConnectionString(iotHubDeviceConnStr);
            Twin twin = new Twin();
            try
            {
                twin = await deviceClient.GetTwinAsync();
                if (twin.Properties.Desired["pollingInterval"] != PollingInterval)
                {
                    Misc.LogDebug("Setting new pollingInterval: " +
                        $"{twin.Properties.Desired["pollingInterval"]} seconds");
                    try
                    {
                        PollingInterval = twin.Properties.Desired["pollingInterval"];
                    }
                    catch (Exception ex)
                    {
                        Misc.LogException($"Unable to set pollingInterval: {ex.Message}");
                    }
                }

                await deviceClient.SetDesiredPropertyUpdateCallbackAsync(desiredPropertyUpdateCallback, null);
            }
            catch (Exception ex)
            {
                Misc.LogException(ex.Message);
            }
        }

        public Task<TelemetryPoint> GetDataAsync()
        {
            var ct = new CancellationToken();
            Task<TelemetryPoint> t = Task.Run<TelemetryPoint>
            (async () =>
                {
                    string iotHubDeviceId = AppSettings.iotHubDeviceId;
                    short[] testOffset = Array.Empty<short>();

                    try
                    {
                        if (!modbusClientAlive)
                        {
                            modbusClient.Init();
                            modbusClientAlive = true;
                        }

                        testOffset = await modbusClient.ReadRegistersAsync(
                            AppSettings.testOffset, AppSettings.testCount);

                    }
                    catch (Exception ex)
                    {
                        Misc.LogException($"Exception while calling ReadRegistersAsync(): {ex.Message}\n" +
                            $"Stack Trace --\n{ex.StackTrace}");
                        modbusClientAlive = false;

                        return null;
                    }

                    TelemetryPoint sensorData = new TelemetryPoint()
                    {
                        iotHubDeviceId = iotHubDeviceId,
                        testOffset = testOffset
                    };

                    return sensorData;
                }, ct);

            // The Modbus library does not time-bound its calls,
            // abort task if execution takes longer than 14 seconds.
            if (t.Wait(14000, ct))
            {
                t.Dispose();
                return t;
            }
            Misc.LogTimeout("Reading data from Modbus took too long. Aborting task.");

            return Task.FromResult<TelemetryPoint>(null);
        }

        public async Task<bool> SendMessageToIotHubAsync(object data)
        {
            Misc.LogDebug("Serialized telemetry object:\n" +
                              JsonConvert.SerializeObject(data, Formatting.Indented));

            try
            {
                var payload = Encoding.ASCII.GetBytes(JsonConvert.SerializeObject(data));
                var message = new Message(payload);

                Misc.LogDebug("Sending message to Azure IoT Hub...");
                await deviceClient.SendEventAsync(message);
                Misc.LogDebug($"Sent was successful at {DateTime.Now.ToUniversalTime()}");
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                return false;
            }
            await Spinner.SleepSpinner(PollingInterval);

            return true;
        }
    }
}