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
        internal static IConfiguration config = Misc.ParseConfig();
        internal static string modbusHost = config.GetConnectionString("modbusHost");
        internal static int modbusPort = Convert.ToInt32(config.GetConnectionString("modbusPort"));
        internal int voltageRegisterOffset = Convert.ToInt32(config.GetConnectionString("voltageRegisterOffset"));
        internal int voltageRegisterCount = Convert.ToInt32(config.GetConnectionString("voltageRegisterCount"));
        internal int currentRegisterOffset = Convert.ToInt32(config.GetConnectionString("currentRegisterOffset"));
        internal int currentRegisterCount = Convert.ToInt32(config.GetConnectionString("currentRegisterCount"));
        public static uint PollingInterval = 11;
        internal bool modbusClientAlive = false;

        public DeviceClient Client => deviceClient;

        public async Task InitAsync(
            string host,
            int port,
            string deviceConnStr,
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
                await Misc.WaitFor(TimeSpan.FromSeconds(15));
            }

            deviceClient = DeviceClient.CreateFromConnectionString(deviceConnStr);
            Twin twin = await deviceClient.GetTwinAsync();
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

        public Task<TelemetryPoint> GetDataAsync()
        {
            var ct = new CancellationToken();
            Task<TelemetryPoint> t = Task.Run<TelemetryPoint>
            (async () =>
                {
                    string iotHubDeviceId = config.GetConnectionString("iotHubDeviceId");
                    short[] voltage = Array.Empty<short>();
                    short[] current = Array.Empty<short>();

                    try
                    {
                        if (!modbusClientAlive)
                        {
                            modbusClient.Init();
                            modbusClientAlive = true;
                        }
                        voltage = await modbusClient.ReadRegistersAsync(
                            voltageRegisterOffset, voltageRegisterCount);
                        current = await modbusClient.ReadRegistersAsync(
                            currentRegisterOffset, currentRegisterCount);
                    }
                    catch (Exception ex)
                    {
                        Misc.LogException($"Exception while calling ReadRegistersAsync(): {ex.Message}\n" +
                            $"Stack Trace --\n{ex.StackTrace}");
                        modbusClientAlive = false;
                        await Misc.WaitFor(TimeSpan.FromSeconds(15));
                    }

                    TelemetryPoint sensorData = new TelemetryPoint()
                    {
                        iotHubDeviceId = iotHubDeviceId,
                        voltage = voltage,
                        current = current
                    };

                    return sensorData;
                }, ct);

            // The Modbus library does not time-bound its calls,
            // abort task if execution takes longer than 9 seconds.
            if (t.Wait(9000, ct))
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