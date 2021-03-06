﻿using System;
using System.Collections.Generic;
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
            // var backoff = new ExponentialBackoff(
            //     10,
            //     TimeSpan.FromSeconds(10),
            //     TimeSpan.FromSeconds(60),
            //     TimeSpan.FromSeconds(15)
            // );
            // IRetryPolicy retryPolicy = backoff;
            // deviceClient.SetRetryPolicy(retryPolicy);
            
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

        public async Task<TelemetryPoint> GetDataAsync()
        {
            string iotHubDeviceId = AppSettings.iotHubDeviceId;
            var analogInput = new Dictionary<string, float>();

            try
            {
                if (!modbusClientAlive)
                {
                    modbusClient = new ModbusClient(
                        AppSettings.modbusHost,
                        AppSettings.modbusPort);
                    modbusClient.Init();
                    modbusClientAlive = true;
                }

                foreach (ModbusAnalogInput input in AppSettings.modbusAnalogInput)
                {
                    var analogInputReading = await modbusClient.ReadRegistersFloatsAsync(
                        input.offset,
                        input.count,
                        AppSettings.unitIdentifier);

                    string semiFloat = String.Format("{0:0.0000}", analogInputReading[0]);
                    var value = float.Parse(
                        semiFloat,
                        System.Globalization.NumberStyles.Any);

                    analogInput.Add(
                        input.label,
                        value);

                }
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
                analogInput = analogInput
            };

            return sensorData;
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