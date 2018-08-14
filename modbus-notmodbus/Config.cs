using System;
using System.IO;
using Microsoft.Extensions.Configuration;

namespace modbus_notmodbus
{
    public static class AppSettings
    {
        public static string iotHubDeviceId;
        public static string iotHubDeviceConnStr;
        public static string modbusHost;
        public static int modbusPort;
        public static int temperatureInputOffset;
        public static int temperatureInputCount;
        public static int digitalInputOffset;
        public static int digitalInputCount;
        public static byte unitIdentifier;
        public static uint pollingInterval;

        public static bool ParseAppSettings()
        {
            var builder = new ConfigurationBuilder()
                    .SetBasePath(Directory.GetCurrentDirectory())
                    .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
                    .AddEnvironmentVariables();

            IConfiguration config = builder.Build();

            string setting = String.Empty;
            try
            {
                setting = "iotHubDeviceId";
                iotHubDeviceId = config.GetConnectionString("iotHubDeviceId");
                setting = "iotHubDeviceConnStr";
                iotHubDeviceConnStr = config.GetConnectionString("iotHubDeviceConnStr");
                setting = "modbusHost";
                modbusHost = config.GetConnectionString("modbusHost");
                setting = "modbusPort";
                modbusPort = Convert.ToInt32(config.GetConnectionString("modbusPort"));
                if (modbusPort >= 65535 || modbusPort < 1)
                {
                        throw new ArgumentException();
                }
                setting = "temperatureInputOffset";
                temperatureInputOffset = Convert.ToInt32(config.GetConnectionString("temperatureInputOffset"));
                setting = "temperatureInputCount";
                temperatureInputCount = Convert.ToInt32(config.GetConnectionString("temperatureInputCount"));
                setting = "digitalInputOffset";
                digitalInputOffset = Convert.ToInt32(config.GetConnectionString("digitalInputOffset"));
                setting = "digitalInputCount";
                digitalInputCount = Convert.ToInt32(config.GetConnectionString("digitalInputCount"));
                setting = "unitIdentifier";
                unitIdentifier = Convert.ToByte(config.GetConnectionString("unitIdentifier"));
                setting = "pollingInterval";
                pollingInterval = Convert.ToUInt16(config.GetConnectionString("pollingInterval"));
            }
            catch (Exception ex)
            {
                Misc.LogException($"\nBroken configuration: {ex.Message}\nCheck {setting}.");
                Environment.Exit(255);
            }

            return true;
        }
    }

}