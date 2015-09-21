using Microsoft.Dnx.Runtime;
using Microsoft.Framework.Configuration;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace StressApp
{
    public class Program
    {
        private IServiceProvider _services;

        public Program(IServiceProvider services)
        {
            _services = services;
        }

        public void Main(string[] args)
        {
            var serverThread = new Thread(() =>
            {
                new Microsoft.AspNet.Hosting.Program(_services).Main(new[] {
                    "--server", "Microsoft.AspNet.Server.Kestrel"
                });
            });
            serverThread.Start();

            Thread.Sleep(2000);
            AddStress();
        }

        byte[] _request = Encoding.ASCII.GetBytes("GET / HTTP/1.1\r\nHost: localhost\r\n\r\n");
        byte[] _response = Encoding.ASCII.GetBytes("HTTP/1.1 200 OK\r\nContent - Length: 11\r\nContent - Type: text / plain\r\nServer: Kestrel\r\nr\nHello world");

        public void AddStress()
        {
            var tasks = new List<Task>();
            for (var index = 0; index != 32; ++index)
            {
                tasks.Add(SendRequestTightLoop());
            }
            for (var index = 0; index != 256; ++index)
            {
                tasks.Add(SendRequestPipelined());
            }
            for (var index = 0; index != 4; ++index)
            {
                tasks.Add(SendRequestAndDispose());
            }
            for (var index = 0; index != 4; ++index)
            {
                tasks.Add(SendRequestPipelinedAndDispose());
            }

            try
            {
                Task.WaitAll(tasks.ToArray());
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Tasks failed\r\n{ex}");
            }
            finally
            {
                Console.WriteLine("All tasks complete?");
            }
        }

        public async Task SendRequestTightLoop()
        {
            var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            try
            {
                socket.Connect(IPAddress.Parse("127.0.0.1"), 5000);
                while (true)
                {
                    await Task.Factory.FromAsync(socket.BeginSend(_request, 0, _request.Length, SocketFlags.None, null, null), socket.EndSend);

                    var data = new byte[4096];
                    var length = 0;
                    var received = 0;
                    do
                    {
                        length += await Task.Factory.FromAsync(socket.BeginReceive(data, length, data.Length - length, SocketFlags.None, null, null), socket.EndReceive);
                    }
                    while (length < _response.Length && received != 0);

                    var text = Encoding.ASCII.GetString(data, 0, length);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"SendRequestTightLoop Failed: {ex}");
                throw;
            }
            finally
            {
                socket.Dispose();
            }
        }

        byte[] _requestPipelined = Encoding.ASCII.GetBytes(Enumerable.Range(0, 16).Aggregate("", (accumulated, _) => accumulated + "GET / HTTP/1.1\r\nHost: localhost\r\n\r\n"));
        byte[] _responsePipelined = Encoding.ASCII.GetBytes(Enumerable.Range(0, 16).Aggregate("", (accumulated, _) => accumulated + "HTTP/1.1 200 OK\r\nContent - Length: 11\r\nContent - Type: text / plain\r\nServer: Kestrel\r\n\r\nHello world"));


        public async Task SendRequestPipelined()
        {
            var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            try
            {
                socket.Connect(IPAddress.Parse("127.0.0.1"), 5000);
                while (true)
                {
                    await Task.Factory.FromAsync(socket.BeginSend(_requestPipelined, 0, _requestPipelined.Length, SocketFlags.None, null, null), socket.EndSend);

                    var data = new byte[4096];
                    var length = 0;
                    var received = 0;
                    do
                    {
                        length += await Task.Factory.FromAsync(socket.BeginReceive(data, length, data.Length - length, SocketFlags.None, null, null), socket.EndReceive);
                    }
                    while (length < _responsePipelined.Length && received != 0);

                    var text = Encoding.ASCII.GetString(data, 0, length);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"SendRequestTightLoop Failed: {ex}");
                throw;
            }
            finally
            {
                socket.Dispose();
            }
        }

        public async Task SendRequestAndDispose()
        {
            try
            {
                while (true)
                {
                    var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                    await Task.Factory.FromAsync(socket.BeginConnect, socket.EndConnect, IPAddress.Parse("127.0.0.1"), 5000, null);
                    await Task.Factory.FromAsync(socket.BeginSend(_request, 0, _request.Length, SocketFlags.None, null, null), socket.EndSend);
                    await Task.Factory.FromAsync(socket.BeginDisconnect, socket.EndDisconnect, false, null);
                    socket.Dispose();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"SendRequestAndDispose Failed: {ex}");
                throw;
            }
        }

        public async Task SendRequestPipelinedAndDispose()
        {
            try
            {
                while (true)
                {
                    var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                    await Task.Factory.FromAsync(socket.BeginConnect, socket.EndConnect, IPAddress.Parse("127.0.0.1"), 5000, null);
                    await Task.Factory.FromAsync(socket.BeginSend(_requestPipelined, 0, _requestPipelined.Length, SocketFlags.None, null, null), socket.EndSend);
                    await Task.Factory.FromAsync(socket.BeginDisconnect, socket.EndDisconnect, false, null);
                    socket.Dispose();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"SendRequestAndDispose Failed: {ex}");
                throw;
            }
        }
    }
}
