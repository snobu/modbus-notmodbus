using System;
using System.IO;
using System.Threading.Tasks;
using System.Collections.Generic;
using Microsoft.Azure.Devices.Shared;
using Microsoft.Extensions.Configuration;

namespace modbus_notmodbus
{
    class Program
    {
        static async Task Main(string[] args)
        {
            AppDomain.CurrentDomain.UnhandledException += (sender, eventArgs) =>
                Misc.LogException($"[GLOBAL EXCEPTION HANDLER] {eventArgs.ExceptionObject}");

            Misc.LogInfo("Press CTRL + C to exit the program.");

            IConfiguration config = Misc.ParseConfig();
            ModbusAdvocate modbusAdvocate = new ModbusAdvocate();

            int modbusPort = Convert.ToInt32(config.GetConnectionString("modbusPort"));
            await modbusAdvocate.InitAsync(config.GetConnectionString("modbusHost"),
                                       modbusPort,
                                       config.GetConnectionString("iotHubDeviceConnStr"),
                                       PropertyUpdateCallback);

            while (true)
            {
                TelemetryPoint sensorData = await modbusAdvocate.GetDataAsync();
                if (sensorData != null)
                    await modbusAdvocate.SendMessageToIotHubAsync(sensorData);
                else
                    await Misc.WaitFor(TimeSpan.FromSeconds(15));
            }

            async Task PropertyUpdateCallback(TwinCollection twinProperties, object userContext)
            {
                Console.WriteLine();
                foreach (var prop in twinProperties)
                {
                    var pair = (KeyValuePair<string, object>)prop;
                    Misc.LogDebug($"desiredProp: {pair.Key} = {pair.Value}");
                }

                if (twinProperties["pollingInterval"] != ModbusAdvocate.PollingInterval)
                {
                    Misc.LogDebug($"Setting new pollingInterval: {twinProperties["pollingInterval"]}");
                    try
                    {
                        ModbusAdvocate.PollingInterval = twinProperties["pollingInterval"];
                    }
                    catch (Exception ex)
                    {
                        Misc.LogException($"Unable to set pollingInterval: {ex.Message}");
                    }
                }

                var reportedProperties = new TwinCollection { ["pollingInterval"] = ModbusAdvocate.PollingInterval };
                await modbusAdvocate.Client.UpdateReportedPropertiesAsync(reportedProperties);
            }

        }
    }
}