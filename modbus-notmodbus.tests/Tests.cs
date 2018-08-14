using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using Shouldly;

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
            string v4Loopback = ("127.0.0.1");

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

    public class DummyTests
    {
        [Fact]
        public void Dummy1()
        {
            (3+2).ShouldBeOfType<int>().ShouldBeGreaterThan(1);
            (1234).ShouldNotBeAssignableTo<UInt32>();
        }
    }
}