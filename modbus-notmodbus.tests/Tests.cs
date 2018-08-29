using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using modbus_notmodbus;
using Shouldly;
using Xunit.Abstractions;
using System.Text;

namespace ModbusTcp.Tests
{
    public class SocketTests
    {
        [Fact]
        public void SocketTimeoutTest()
        {
            Console.WriteLine("Running SocketTimeoutTest...");
            int TEST_TIMEOUT = 6000;
            int MODBUS_CALL_TIMEOUT = 3000;

            Int32 port = 50222;
            string v4Loopback = "127.0.0.1";

            TcpListener server = new TcpListener(IPAddress.Parse(v4Loopback), port);
            server.Start();

            CancellationTokenSource cts = new CancellationTokenSource(TEST_TIMEOUT);
            CancellationToken ct = cts.Token;

            var t = Task.Run(() =>
            {
                server.AcceptTcpClientAsync();
                ModbusClient mc = new ModbusClient(v4Loopback, port, MODBUS_CALL_TIMEOUT);
                mc.Init();
                short[] r = mc.ReadRegistersAsync(4304, 2, 1).Result;
            }, ct);

            try
            {
                t.Wait(ct);
            }
            catch (OperationCanceledException)
            {
                Assert.True(false, "The call to ReadRegistersAsync() did not return " +
                    $"within expected time window of {MODBUS_CALL_TIMEOUT / 1000} seconds.");
            }
            catch (Exception ex)
            {
                if (ex.InnerException.InnerException is TimeoutException ||
                    ex.InnerException.InnerException is IOException)
                {
                    // We expect a TimeoutException or IOException if ReadRegistersAsync() adheres to our timeout
                    Assert.True(true);
                }
                else throw;
            }
            finally
            {
                server.Server.Close();
                server.Server.Dispose();
            }
        }
    }

    public class NumberConversionTests
    {
        private readonly ITestOutputHelper output;

        public NumberConversionTests(ITestOutputHelper output)
        {
            this.output = output;
        }

        [Fact]
        // Check if we can read extreme values from wire with ModbusTcp library
        public void ExtremeValueTest()
        {
            byte[] mockResponseBytes = new byte[] { 0x00, 0x00, 0x00, 0x00, 0x00, 0x07, 0xFF, 0x04, 0x04, 0x7F, 0x7D, 0x40, 0x23 };
            float compareBytes = float.Parse("3.366277E+38", System.Globalization.NumberStyles.Any);
            // Let's mock a Modbus server (slave)
            Int32 port = 50223;
            string v4Loopback = "127.0.0.1";
            TcpListener server = new TcpListener(IPAddress.Parse(v4Loopback), port);

            server.Start();
            float[] reading = new float[1];

            var t = Task.Run(() =>
            {
                ModbusClient modbusClient = new ModbusClient(v4Loopback, port);
                modbusClient.Init();
                reading = modbusClient.ReadRegistersFloatsAsync(100, 2, 0x00).GetAwaiter().GetResult();
            });
            var tcpClient = server.AcceptTcpClient();

            NetworkStream stream = tcpClient.GetStream();

            stream.Write(mockResponseBytes, 0, mockResponseBytes.Length);
            stream.Flush();

            t.Wait();

            var whatWeExpect = BitConverter.GetBytes(compareBytes);
            var whatWeGot = BitConverter.GetBytes(reading[0]);

            string compare = String.Empty;
            for (var i = whatWeExpect.Length-1; i >= 0; i--)
            {
                compare += String.Format("x{0:X}", whatWeExpect[i]);
            }

            output.WriteLine(compare);
            compare = String.Empty;

            for (var i = whatWeGot.Length-1; i >= 0; i--)
            {
                compare += String.Format("y{0:X}", whatWeGot[i]);
            }

            output.WriteLine(compare);
            Assert.Equal(compareBytes, reading[0]);
        }
    }
}