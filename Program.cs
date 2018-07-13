using System;
using System.Text;
using Microsoft.Azure.Devices.Client;
using Microsoft.Azure.Devices.Shared;
using Newtonsoft.Json;
using ModbusTcp;
using System.Threading.Tasks;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using Microsoft.Extensions.Configuration;
using System.IO;
using System.Threading;

namespace modbus_notmodbus
{
    class Program
    {
        static IConfiguration config = LoadConfig();

        public static uint pollingInterval = 11; // seconds
        public static string deviceId = "ModbusCollector";
        public static string modbusHost = config.GetConnectionString("modbusHost");
        public static int modbusPort = Convert.ToInt16(config.GetConnectionString("modbusPort"));
        public static string deviceConnStr = config.GetConnectionString("deviceConnStr");
        static DeviceClient c;

        static async Task Main(string[] args)
        {
            //#if !DEBUG
            AppDomain.CurrentDomain.UnhandledException += CallBombSquad;
            //#endif
            ModbusClient m = new ModbusClient(modbusHost, modbusPort);
            try
            {
                m.Init();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[EXCEPTION] Exception while instantiating Modbus client: {ex.Message}");
                //#if !DEBUG
                Environment.Exit(-1);
                //#endif
            }

            c = DeviceClient.CreateFromConnectionString(deviceConnStr);
            Twin twin = await c.GetTwinAsync();
            if (twin.Properties.Desired["pollingInterval"] != Program.pollingInterval)
            {
                Console.WriteLine("[DEBUG] Setting new pollingInterval: " +
                    $"{twin.Properties.Desired["pollingInterval"]} seconds");
                try
                {
                    Program.pollingInterval = twin.Properties.Desired["pollingInterval"];
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[EXCEPTION] Unable to set pollingInterval: {ex.Message}");
                }
            }
            await c.SetDesiredPropertyUpdateCallbackAsync(OnDesiredPropertyChanged, null);

            while (true)
            {
                var tf = new TaskFactory();
                object telemetryObject = null;
                Console.WriteLine("block for 5 sec");
                await tf.ContinueWhenAll(new []{ GetModbusData(m, telemetryObject)}, d => {
                    Console.WriteLine("continuation...");});

                Console.WriteLine("[DEBUG] Serialized telemetry object:\n" +
                    JsonConvert.SerializeObject(telemetryObject, Formatting.Indented));

                byte[] payload = Encoding.ASCII.GetBytes(JsonConvert.SerializeObject(telemetryObject));
                Message message = new Message(payload);

                Console.WriteLine("Sending message to Azure IoT Hub...");
                await c.SendEventAsync(message);
                Console.WriteLine("OK");

                await Spinner.SleepSpinner(pollingInterval);
            }
        }

        private static void CallBombSquad(object sender, UnhandledExceptionEventArgs e)
        {
            Console.WriteLine($"[BombSquad][EXCEPTION] {e.ToString()}");
        }

        static IConfiguration LoadConfig()
        {
            IConfigurationBuilder builder = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
                .AddEnvironmentVariables();

            IConfiguration Configuration = builder.Build();

            return Configuration;
        }

        static Task GetModbusData(ModbusClient m, object data)
        {
            Console.WriteLine("Fetching MODBUS data...");
            Task t = null;
            Console.WriteLine("starting timer...");
            System.Threading.Timer ti = new System.Threading.Timer(tc => {
                t = Task.Run(() =>
                {
                    Console.WriteLine("in task run");
                    //short[] voltage = await m.ReadRegistersAsync(40001, 3);
                    //short[] current = await m.ReadRegistersAsync(41001, 3);
                    string hardwareId = "Function Code 0x2b (43)";
                    return Task.FromResult 
                    (
                        data = new {
                            //deviceId = deviceId,
                            //voltage = voltage,
                            //current = current,
                            hardwareId = hardwareId
                        });
                });
            });
            Console.WriteLine("outside task run");
            ti.Change(5000, -1);
            

            return t;
        }


        // System.Diagnostics.Process.Start(System.AppDomain.CurrentDomain.FriendlyName);
        // Environment.Exit(-2);

        static async Task OnDesiredPropertyChanged(TwinCollection desiredProperties, object userContext)
        {
            Console.WriteLine();
            foreach (var prop in desiredProperties)
            {
                var pair = (KeyValuePair<string, object>)prop;
                var value = pair.Value as JValue;
                Console.WriteLine($"[DEBUG] desiredProp: {pair.Key} = {pair.Value.ToString()}");
            }

            if (desiredProperties["pollingInterval"] != Program.pollingInterval)
            {
                Console.WriteLine($"[DEBUG] Setting new pollingInterval: {desiredProperties["pollingInterval"]}");
                try
                {
                    Program.pollingInterval = desiredProperties["pollingInterval"];
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[EXCEPTION] Unable to set pollingInterval: {ex.Message}");
                }
            }

            TwinCollection reportedProperties = new TwinCollection();
            reportedProperties["pollingInterval"] = Program.pollingInterval;
            await c.UpdateReportedPropertiesAsync(reportedProperties);
        }
    }
}