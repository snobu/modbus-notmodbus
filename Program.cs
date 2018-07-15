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

            AppSettings.ParseAppSettings();

            Misc.LogInfo("Press CTRL + C to exit the program.");
            
            ModbusAdvocate modbusAdvocate = new ModbusAdvocate();
            
            await modbusAdvocate.InitAsync(
                AppSettings.modbusHost,
                AppSettings.modbusPort,
                AppSettings.iotHubDeviceConnStr,
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