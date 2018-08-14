using System;
using System.Net.Sockets;
using ModbusTcp.Protocol.Request;
using System.Threading.Tasks;
using ModbusTcp.Protocol;
using System.Linq;
using ModbusTcp.Protocol.Reply;
using System.Collections.Generic;
using System.Net;
using System.Threading;
using System.Collections;

namespace ModbusTcp
{
    public class ModbusClient
    {
        private int socketTimeout;
        private readonly int port;
        private TcpClient tcpClient;
        private NetworkStream transportStream;
        private readonly string ipAddress;

        public ModbusClient(string ipAddress, int port, int socketTimeout = 10000)
        {
            this.ipAddress = ipAddress;
            this.port = port;
            this.socketTimeout = socketTimeout;
        }

        public void Init()
        {
            try
            {
                tcpClient = new TcpClient(ipAddress, port);
                transportStream = tcpClient.GetStream();
            }
            catch (Exception e)
            {
                Console.WriteLine($"Failed to open TCP client - {e}");
                throw e;
            }
        }

        /// <summary>
        /// Reads words holding registers
        /// </summary>
        /// <param name="offset">The register offset</param>
        /// <param name="count">Number of words to read</param>
        /// <returns>The words read</returns>
        public async Task<short[]> ReadRegistersAsync(int offset, int count, byte unitIdentifier)
        {
            if (tcpClient == null)
                throw new Exception("Object not intialized");


            var request = new ModbusRequest04(offset, count, unitIdentifier);
            var buffer = request.ToNetworkBuffer();

            using (var cancellationTokenSource = new CancellationTokenSource(socketTimeout))
            {
                using(cancellationTokenSource.Token.Register(() => transportStream.Close()))
                {
                    await transportStream.WriteAsync(buffer, 0, buffer.Length, cancellationTokenSource.Token);
                }
            }

            var response = await ReadResponseAsync<ModbusReply04>();
            return ReadAsShort(response.Data);
        }

        /// <summary>
        /// Reads digital input status (Function Code 02)
        /// </summary>
        /// <param name="offset">The register offset</param>
        /// <param name="count">Number of words to read</param>
        /// <returns>Digital inputs on/off status as array of bools</returns>
        public async Task<bool[]> ReadInputStatusAsync(int offset, int count, byte unitIdentifier)
        {
            if (tcpClient == null)
                throw new Exception("Object not intialized");


            var request = new ModbusRequest02(offset, count, unitIdentifier);
            var buffer = request.ToNetworkBuffer();

            using (var cancellationTokenSource = new CancellationTokenSource(socketTimeout))
            {
                using(cancellationTokenSource.Token.Register(() => transportStream.Close()))
                {
                    await transportStream.WriteAsync(buffer, 0, buffer.Length, cancellationTokenSource.Token);
                }
            }

            var response = await ReadResponseAsync<ModbusReply02>();

            Console.BackgroundColor = ConsoleColor.DarkGray;

            Console.Write($"\n[DEBUG] Digital Input Status request bytes:  ");
            foreach (byte b in buffer)
            {
                Console.Write($"{b.ToString("X2")}  ");
            }

            Console.Write($"\n[DEBUG] Digital Input Status response (value) bytes:  ");
            foreach (byte b in response.Data)
            {
                Console.Write($"{b.ToString("X2")}  ");
            }

            Console.ResetColor();

            return ReadAsBool(response.Data);
        }

        /// <summary>
        /// Reads floats from holding registers
        /// </summary>
        /// <param name="offset">The register offset</param>
        /// <param name="count">Number of floats to read</param>
        /// <returns>The floats read</returns>
        public async Task<float[]> ReadRegistersFloatsAsync(int offset, int count, byte unitIdentifier)
        {
            if (tcpClient == null)
                throw new Exception("Object not intialized");

            // Float is 2 word (expect count x 2)
            var request = new ModbusRequest04(offset, count, unitIdentifier);

            var buffer = request.ToNetworkBuffer();
            using (var cancellationTokenSource = new CancellationTokenSource(socketTimeout))
            {
                using(cancellationTokenSource.Token.Register(() => transportStream.Close()))
                {
                    await transportStream.WriteAsync(buffer, 0, buffer.Length, cancellationTokenSource.Token);
                }
            }

            var response = await ReadResponseAsync<ModbusReply04>();

            Console.BackgroundColor = ConsoleColor.DarkGray;

            Console.Write($"\n[DEBUG] Analog Input request bytes:  ");
            foreach (byte b in buffer)
            {
                Console.Write($"{b.ToString("X2")}  ");
            }

            Console.Write($"\n[DEBUG] Analog Input response (value) bytes:  ");
            foreach (byte b in response.Data)
            {
                Console.Write($"{b.ToString("X2")}  ");
            }

            Console.ResetColor();
            
            return ReadAsFloat(response.Data);
        }

        /// <summary>
        /// Terminates the session
        /// </summary>
        public void Terminate()
        {
            tcpClient.Close();
            tcpClient = null;
        }

        private short[] ReadAsShort(byte[] data)
        {
            var idx = 0;
            var output = new List<short>();

            while (idx < data.Length)
            {
                var value = IPAddress.NetworkToHostOrder(BitConverter.ToInt16(data, idx));
                idx += 2;

                output.Add(value);
            }

            return output.ToArray();
        }

        private float[] ReadAsFloat(byte[] data)
        {
            var idx = 0;
            var output = new List<float>();

            while (idx < data.Length)
            {
                var value = IPAddress.NetworkToHostOrder(BitConverter.ToInt32(data, idx));
                var f = BitConverter.ToSingle(BitConverter.GetBytes(value), 0);
                idx += 4;

                output.Add(f);
            }

            return output.ToArray();
        }

        private bool[] ReadAsBool(byte[] data)
        {
            var output = new List<bool>();
            BitArray bitArray = new BitArray(data);
            for (int i =0 ; i < bitArray.Length; i++)
            {
                output.Add(bitArray.Get(i));
            }

            return output.ToArray();
        }

        private async Task<T> ReadResponseAsync<T>() where T : ModbusReponseBase
        {
            var headerBytes = await ReadFromBuffer(ModbusHeader.FixedLength);
            var header = ModbusHeader.FromNetworkBuffer(headerBytes);

            var dataBytes = await ReadFromBuffer(header.Length);

            var fullBuffer = headerBytes.Concat(dataBytes).ToArray();
            var response = Activator.CreateInstance<T>();
            response.FromNetworkBuffer(fullBuffer);

            return response;
        }

        private async Task<byte[]> ReadFromBuffer(int totalSize)
        {
            var buffer = new byte[totalSize];

            var idx = 0;
            var remainder = totalSize;

            while (remainder > 0)
            {
                int readBytes = 0;
                using (var cancellationTokenSource = new CancellationTokenSource(socketTimeout))
                {
                    using(cancellationTokenSource.Token.Register(() => transportStream.Close()))
                    {
                        readBytes = await transportStream.ReadAsync(buffer, idx, remainder, cancellationTokenSource.Token);
                    }
                }
                remainder -= readBytes;
                idx += readBytes;

                if (readBytes == 0)
                    throw new SocketException((int)SocketError.ConnectionReset);
            }

            return buffer;
        }
    }
}
