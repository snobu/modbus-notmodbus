using System;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.Devices.Client;
using ModbusTcp;
using Newtonsoft.Json;

namespace modbus_notmodbus
{
    public class ModbusOperator
    {
        internal      ModbusClient modbusClient;
        internal      DeviceClient deviceClient;
        public static uint         PoolingInterval = 11;
        private string deviceId;
        internal bool modbusClientAlive = false;

        public DeviceClient Client => deviceClient;

        public async Task InitAsync(string                        host, int port,
                                    string                        deviceConnStr,
                                    DesiredPropertyUpdateCallback desiredPropertyUpdateCallback,
                                    string                        currentDeviceid = "modbusdevice")
        {
            modbusClient  = new ModbusClient(host, port);
            deviceId = currentDeviceid;
            try
            {
                modbusClient.Init();
                modbusClientAlive = true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[EXCEPTION] Exception while instantiating Modbus client: {ex.Message}");
                Environment.Exit(-1);
            }

            deviceClient = DeviceClient.CreateFromConnectionString(deviceConnStr);
            var twin = await deviceClient.GetTwinAsync();
            if (twin.Properties.Desired["pollingInterval"] != PoolingInterval)
            {
                Console.WriteLine("[DEBUG] Setting new pollingInterval: " +
                                  $"{twin.Properties.Desired["pollingInterval"]} seconds");
                try
                {
                    PoolingInterval = twin.Properties.Desired["pollingInterval"];
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[EXCEPTION] Unable to set pollingInterval: {ex.Message}");
                }
            }

            await deviceClient.SetDesiredPropertyUpdateCallbackAsync(desiredPropertyUpdateCallback, null);
        }

        public Task<object> GetDataAsync()
        {
            while (!modbusClientAlive)
            {
                try
                {
                    modbusClient.Init();
                    modbusClientAlive = true;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[EXCEPTION] Exception while instantiating Modbus client: {ex.Message}");
                    Console.WriteLine("\nSleeping for 15 seconds before retrying to talk to Modbus host...\n");
                    Task.Delay(TimeSpan.FromSeconds(15)).Wait();
                }
            }

            var ct = new CancellationToken();
            Task<object> t = Task.Run<object>(async () =>
                                              {
                                                  var voltage    = Array.Empty<short>();
                                                  var current    = Array.Empty<short>();
                                                  var hardwareId = String.Empty;
                                                  try
                                                  {
                                                      voltage    = await modbusClient.ReadRegistersAsync(40001, 3);
                                                      current    = await modbusClient.ReadRegistersAsync(41001, 3);
                                                      hardwareId = "Function Code 0x2b (43)";
                                                  }
                                                  catch (Exception ex)
                                                  {
                                                      Console.WriteLine(
                                                          $"[EXCEPTION] Exception while calling ReadRegistersAsync(): {ex.Message}");
                                                      Console.WriteLine(
                                                          "\nSleeping for 5 seconds before retrying...\n");
                                                      Task.Delay(TimeSpan.FromSeconds(5), ct).Wait(ct);
                                                  }

                                                  return new
                                                         {
                                                             deviceId,
                                                             voltage,
                                                             current,
                                                             hardwareId
                                                         };
                                              }, ct);

            if (t.Wait(9000, ct))
            {
                return t;
            }

            Console.WriteLine("Aborting modbus Task, took too long to return.");
            //Console.WriteLine("Restarting collector...\n");
            //Assembly a = Assembly.GetExecutingAssembly();
            //System.Diagnostics.Process.Start("dotnet.exe", a.Location);
            //Environment.Exit(-2);
            return null;
        }

        public async Task<bool> SendMessageToIotHubAsync(object data)
        {
            Console.WriteLine("[DEBUG] Serialized telemetry object:\n" +
                              JsonConvert.SerializeObject(data, Formatting.Indented));

            try
            {
                var payload = Encoding.ASCII.GetBytes(JsonConvert.SerializeObject(data));
                var message = new Message(payload);

                Console.WriteLine("Sending message to Azure IoT Hub...");
                await deviceClient.SendEventAsync(message);
                Console.WriteLine($"Sent was successfull at {DateTime.Now.ToUniversalTime()}");
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                return false;
            }

            await Spinner.SleepSpinner(PoolingInterval);
            return true;
        }
    }
}