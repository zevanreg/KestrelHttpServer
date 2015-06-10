using Microsoft.AspNet.Server.Kestrel.Networking;
using Microsoft.Framework.Runtime;
using System;
using System.IO;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Text;
using System.Diagnostics;
using System.Threading;
using System.Collections.Generic;
using System.Linq;

namespace TcpEcho
{
    public class Program
    {
        private readonly Libuv uv = new Libuv();
        private readonly UvLoopHandle loop = new UvLoopHandle();

        private readonly Action<UvStreamHandle, int, Exception, object> _onTcpListen;
        private readonly Func<UvStreamHandle, int, object, Libuv.uv_buf_t> _onAlloc;
        private readonly Action<UvStreamHandle, int, Exception, object> _onRead;
        private readonly Action<UvWriteReqNative, int, Exception, object> _onWrite;

        private readonly Action<UvStreamHandle, int, Exception, object> _onHttpListen;
        private readonly Func<UvStreamHandle, int, object, Libuv.uv_buf_t> _onHttpAlloc;
        private readonly Action<UvStreamHandle, int, Exception, object> _onHttpRead;
        private readonly Action<UvWriteReqNative, int, Exception, object> _onHttpWrite;

        private readonly List<Worker> _workers = new List<Worker>();

        static readonly string responseStr = "HTTP/1.1 200 OK\r\n" +
            "Content-Type: text/plain;charset=UTF-8\r\n" +
            "Content-Length: 10\r\n" +
            "Connection: keep-alive\r\n" +
            "Server: Dummy\r\n" +
            "\r\n" +
            "HelloWorld";


        private static byte[] _responseBytes = Encoding.UTF8.GetBytes(responseStr);
        private static IntPtr _responseBuffer;

        private const string WS2_32 = "ws2_32.dll";

        [Flags]
        internal enum SocketConstructorFlags
        {
            WSA_FLAG_OVERLAPPED = 0x01,
            WSA_FLAG_MULTIPOINT_C_ROOT = 0x02,
            WSA_FLAG_MULTIPOINT_C_LEAF = 0x04,
            WSA_FLAG_MULTIPOINT_D_ROOT = 0x08,
            WSA_FLAG_MULTIPOINT_D_LEAF = 0x10,
        }

        // CharSet=Auto here since WSASocket has A and W versions. We can use Auto cause the method is not used under constrained execution region
        [DllImport(WS2_32, CharSet = CharSet.Auto, SetLastError = true)]
        internal static extern IntPtr WSASocket(
            [In] AddressFamily addressFamily,
            [In] SocketType socketType,
            [In] ProtocolType protocolType,
            [In] IntPtr protocolInfo, // will be WSAProtcolInfo protocolInfo once we include QOS APIs
            [In] uint group,
            [In] SocketConstructorFlags flags
        );

        [DllImport(WS2_32, CharSet = CharSet.Auto, SetLastError = true)]
        internal unsafe static extern IntPtr WSASocket(
            [In] AddressFamily addressFamily,
            [In] SocketType socketType,
            [In] ProtocolType protocolType,
            [In] byte* pinnedBuffer, // will be WSAProtcolInfo protocolInfo once we include QOS APIs
            [In] uint group,
            [In] SocketConstructorFlags flags
        );

        [DllImport(WS2_32, SetLastError = true)]
        internal unsafe static extern int WSADuplicateSocket(
            [In] IntPtr socketHandle,
            [In] uint targetProcessID,
            [In] byte* pinnedBuffer
        );

        public Program(ILibraryManager libraryManager)
        {
            _onTcpListen = OnListen;
            _onAlloc = OnAlloc;
            _onRead = OnRead;
            _onWrite = OnWrite;

            _onHttpListen = OnHttpListen;
            _onHttpAlloc = OnHttpAlloc;
            _onHttpRead = OnHttpRead;
            _onHttpWrite = OnHttpWrite;

            LoadLibuv(libraryManager);

            _responseBuffer = Marshal.AllocCoTaskMem(_responseBytes.Length);
            Marshal.Copy(_responseBytes, 0, _responseBuffer, _responseBytes.Length);
        }

        Thread logging;
        public static int mode;

        public unsafe void Main(string[] args)
        {
            loop.Init(uv);

            mode = (args.Length == 1) ? int.Parse(args[0]) : 0;

            Console.WriteLine($"starting mode {mode}");

            var work = new UvAsyncHandle();
            work.Init(loop, () =>
            {
                //var tcpListen = new UvTcpHandle();
                //tcpListen.Init(loop);
                //tcpListen.Bind(new System.Net.IPEndPoint(0, 5001));
                //tcpListen.Listen(10, _onTcpListen, null);

                //var httpListen1 = new UvTcpHandle();
                //httpListen1.Init(loop);
                ////httpListen1.Open(socketHandle1);
                //httpListen1.Bind(new System.Net.IPEndPoint(0, 5004));
                //httpListen1.Listen(10, _onHttpListen, "Listen1");

                var httpListenCluster = new UvTcpHandle();
                httpListenCluster.Init(loop);
                httpListenCluster.Bind(new System.Net.IPEndPoint(0, 5005 + mode));

                var pipe = new UvPipeHandle();
                pipe.Init(loop, false);
                pipe.Bind("\\\\?\\pipe\\uv-test" + mode);
                pipe.Listen(128, OnPipeListen, httpListenCluster);

                Console.WriteLine($"Starting {Environment.ProcessorCount} workers");

                for (var index = 0; index != Environment.ProcessorCount; ++index)
                {
                    if (mode == 0 || (mode == 1 && (index % 2 == 0)) || (mode == 2 && (index % 2 == 1)))
                    {
                        var worker = new Worker(uv, 5010 + index);
                        worker.Start();
                        _workers.Add(worker);
                    }
                }

                (logging = new Thread(() =>
                {
                    for (; ;)
                    {
                        Thread.Sleep(TimeSpan.FromSeconds(4));
                        Console.WriteLine(_workers.Aggregate("", (a, b) => $"{a} {b.Connections}"));
                    }
                })).Start();

                Console.WriteLine($"Started {_workers.Count} workers");
            });

            work.Send();

            Console.WriteLine("Server started...");

            loop.Run();
            work.Dispose();
            loop.Dispose();
        }

        public void OnPipeListen(
            UvStreamHandle pipe,
            int status,
            Exception error,
            object state)
        {
            Console.WriteLine("Main.OnPipeListen");

            var httpListenCluster = (UvTcpHandle)state;

            try
            {
                var workerPipe = new UvPipeHandle();
                workerPipe.Init(loop, true);
                pipe.Accept(workerPipe);
                var writeRequest = new UvWriteReqNative();
                writeRequest.Init(loop);
                writeRequest.Write2(workerPipe, _responseBuffer, 1, httpListenCluster, OnPipeWrite, workerPipe);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
        }

        public void OnPipeWrite(
            UvWriteReqNative request,
            int status,
            Exception error,
            object state)
        {
            var workerPipe = (UvPipeHandle)state;
            request.Dispose();
            workerPipe.Dispose();
        }

        public void OnListen(
            UvStreamHandle tcpListen,
            int status,
            Exception error,
            object state)
        {
            var tcpStream = new UvTcpHandle();
            tcpStream.Init(loop);
            tcpListen.Accept(tcpStream);
            tcpStream.ReadStart(
                _onAlloc,
                _onRead,
                new ReadState());
        }

        public Libuv.uv_buf_t OnAlloc(
            UvStreamHandle tcpStream,
            int suggestedSize,
            object state)
        {
            var readState = (ReadState)state;
            readState.Buffer = Marshal.AllocCoTaskMem(suggestedSize);
            return uv.buf_init(readState.Buffer, suggestedSize);
        }

        public void OnRead(
            UvStreamHandle tcpStream,
            int status,
            Exception error,
            object state)
        {
            var readState = (ReadState)state;

            var normalRead = error == null && status > 0;
            var normalDone = status == 0 || status == -4077 || status == -4095;
            var errorDone = !(normalDone || normalRead);

            if (normalRead)
            {
                var writeState = new WriteState();
                writeState.Buffer = readState.Buffer;
                readState.Buffer = IntPtr.Zero;

                var tcpWrite = new UvWriteReqNative();
                tcpWrite.Init(loop);
                tcpWrite.Write(
                    tcpStream,
                    writeState.Buffer,
                    status,
                    _onWrite,
                    writeState);
            }
            else
            {
                Marshal.FreeCoTaskMem(readState.Buffer); //TODO: pool
                tcpStream.Dispose();
            }
        }

        private void OnWrite(
            UvWriteReqNative tcpWrite,
            int status,
            Exception error,
            object state)
        {
            var tcpStreamWrite = (WriteState)state;
            Marshal.FreeCoTaskMem(tcpStreamWrite.Buffer); //TODO: pool
            tcpWrite.Dispose(); //TODO: pool
        }

        public class ReadState
        {
            public IntPtr Buffer;
        }

        public class WriteState
        {
            public IntPtr Buffer;
        }


        private void OnHttpListen(
            UvStreamHandle httpListen,
            int status,
            Exception error,
            object state)
        {
            var message = (string)state;
            //Console.WriteLine(message);

            var httpStream = new UvTcpHandle();
            httpStream.Init(loop);
            httpListen.Accept(httpStream);

            var httpState = new HttpState();
            httpState.Buffer = Marshal.AllocCoTaskMem(8192);
            httpState.HttpWrite = new UvWriteReqNative();
            httpState.HttpWrite.Init(loop);

            httpStream.ReadStart(
                _onHttpAlloc,
                _onHttpRead,
                httpState);
        }

        private Libuv.uv_buf_t OnHttpAlloc(
            UvStreamHandle httpStream,
            int suggestedSize,
            object state)
        {
            var httpState = (HttpState)state;
            return uv.buf_init(httpState.Buffer, 8192);
        }

        private void OnHttpRead(
            UvStreamHandle httpStream,
            int status,
            Exception error,
            object state)
        {
            var httpState = (HttpState)state;

            var normalRead = error == null && status > 0;
            var normalDone = status == 0 || status == -4077 || status == -4095;
            var errorDone = !(normalDone || normalRead);

            if (normalRead)
            {
                httpState.HttpWrite.Write(
                    httpStream,
                    _responseBuffer,
                    _responseBytes.Length,
                    _onHttpWrite,
                    httpState);
            }
            else
            {
                // TODO: disconnect
            }
        }

        private void OnHttpWrite(
            UvWriteReqNative httpWrite,
            int status,
            Exception error,
            object state)
        {
            //var httpStream = (UvTcpHandle)state;

            //var normalWrite = error == null && status > 0;
            //var normalDone = status == 0 || status == -4077 || status == -4095;
            //var errorDone = !(normalDone || normalWrite);

            //httpWrite.Write(
            //    httpStream,
            //    _responseBuffer,
            //    _responseBytes.Length,
            //    _onHttpWrite,
            //    null);
        }

        class HttpState
        {
            public IntPtr Buffer;
            public UvWriteReqNative HttpWrite;
        }


        private void LoadLibuv(ILibraryManager libraryManager)
        {
            var libraryPath = "";
            if (libraryManager != null)
            {
                var library = libraryManager.GetLibraryInformation("Microsoft.AspNet.Server.Kestrel");
                libraryPath = library.Path;
                if (library.Type == "Project")
                {
                    libraryPath = Path.GetDirectoryName(libraryPath);
                }
                if (uv.IsWindows)
                {
                    var architecture = IntPtr.Size == 4
                        ? "x86"
                        : "amd64";

                    libraryPath = Path.Combine(
                        libraryPath,
                        "native",
                        "windows",
                        architecture,
                        "libuv.dll");
                }
                else if (uv.IsDarwin)
                {
                    libraryPath = Path.Combine(
                        libraryPath,
                        "native",
                        "darwin",
                        "universal",
                        "libuv.dylib");
                }
                else
                {
                    libraryPath = "libuv.so.1";
                }
            }
            uv.Load(libraryPath);
        }
    }
}
