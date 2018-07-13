using System;
using Microsoft.Azure.Devices.Shared;
using System.Threading.Tasks;
using System.Collections.Generic;
using Microsoft.Extensions.Configuration;
using System.IO;

namespace modbus_notmodbus
{
    class Program
    {
        static async Task Main(string[] args)
        {
            AppDomain.CurrentDomain.UnhandledException += (sender, eventArgs) =>
                                                              Console.WriteLine(
                                                                  $"[BombSquad][EXCEPTION] {eventArgs.ExceptionObject}");
            ;

            var builder = new ConfigurationBuilder()
                          .SetBasePath(Directory.GetCurrentDirectory())
                          .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
                          .AddEnvironmentVariables();

            IConfiguration config = builder.Build();

            var mbOperator = new ModbusOperator();

            var modbusPort = Convert.ToInt16(config.GetConnectionString("modbusPort"));

            await mbOperator.InitAsync(config.GetConnectionString("modbusHost"),
                                       modbusPort,
                                       config.GetConnectionString("deviceConnStr"),
                                       PropertyUpdateCallback, "ModbusDeviceId");

            Console.WriteLine("Press CTRL+C to exit the program!");
            while (true)
            {
                var currentObject = await mbOperator.GetDataAsync();
                if (currentObject != null)
                    await mbOperator.SendMessageToIotHubAsync(currentObject);
            }

            async Task PropertyUpdateCallback(TwinCollection twinProperties, object userContext)
            {
                Console.WriteLine();
                foreach (var prop in twinProperties)
                {
                    var pair = (KeyValuePair<string, object>) prop;
                    Console.WriteLine($"[DEBUG] desiredProp: {pair.Key} = {pair.Value}");
                }

                if (twinProperties["pollingInterval"] != ModbusOperator.PoolingInterval)
                {
                    Console.WriteLine($"[DEBUG] Setting new pollingInterval: {twinProperties["pollingInterval"]}");
                    try
                    {
                        ModbusOperator.PoolingInterval = twinProperties["pollingInterval"];
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[EXCEPTION] Unable to set pollingInterval: {ex.Message}");
                    }
                }

                var reportedProperties = new TwinCollection {["pollingInterval"] = ModbusOperator.PoolingInterval};
                await mbOperator.Client.UpdateReportedPropertiesAsync(reportedProperties);
            }
        }
    }
}