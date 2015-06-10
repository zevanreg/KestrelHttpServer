using Microsoft.AspNet.Server.Kestrel.Networking;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace TcpEcho
{
    public class Worker
    {
        Libuv uv;
        Thread thread;
        UvLoopHandle loop;
        int port;

        private readonly Action<UvStreamHandle, int, Exception, object> _onHttpListen;
        private readonly Func<UvStreamHandle, int, object, Libuv.uv_buf_t> _onHttpAlloc;
        private readonly Action<UvStreamHandle, int, Exception, object> _onHttpRead;
        private readonly Action<UvWriteReqNative, int, Exception, object> _onHttpWrite;

        static readonly string responseStr = "HTTP/1.1 200 OK\r\n" +
            "Content-Type: text/plain;charset=UTF-8\r\n" +
            "Content-Length: 10\r\n" +
            "Connection: keep-alive\r\n" +
            "Server: Dummy\r\n" +
            "\r\n" +
            "HelloWorld";


        private static byte[] _responseBytes = Encoding.UTF8.GetBytes(responseStr);
        private static IntPtr _responseBuffer;

        public Worker(Libuv uv, int port)
        {
            this.uv = uv;
            this.port = port;

            _onHttpListen = OnHttpListen;
            _onHttpAlloc = OnHttpAlloc;
            _onHttpRead = OnHttpRead;
            _onHttpWrite = OnHttpWrite;

            _responseBuffer = Marshal.AllocCoTaskMem(_responseBytes.Length);
            Marshal.Copy(_responseBytes, 0, _responseBuffer, _responseBytes.Length);

            Console.WriteLine($"Bytes per response {_responseBytes.Length}");

            thread = new Thread(OnThreadStart);
        }

        public int Connections;

        public void Start()
        {
            thread.Start();
        }

        private void OnThreadStart()
        {
            try
            {
                loop = new UvLoopHandle();
                loop.Init(uv);

                var pipe = new UvPipeHandle();
                pipe.Init(loop, true);

                var connect = new UvConnectRequest();
                connect.Init(loop);
                connect.Connect(pipe, "\\\\?\\pipe\\uv-test", OnPipeConnect, pipe);

                loop.Run();
            }
            catch (Exception ex)
            {
                Console.WriteLine("Worker.OnThreadStart " + ex.Message);
            }
        }

        private void OnPipeConnect(
            UvConnectRequest connect,
            int status,
            Exception error,
            object state)
        {
            var pipe = (UvPipeHandle)state;
            Console.WriteLine($"Worker.OnPipeConnect {status} {error?.Message}");

            if (status == 0)
            {
                pipe.ReadStart(OnPipeAlloc, OnPipeRead, null);
            }
            else
            {
                connect.Connect(pipe, "\\\\?\\pipe\\uv-test" + Program.mode, OnPipeConnect, pipe);
            }
        }

        private Libuv.uv_buf_t OnPipeAlloc(UvStreamHandle arg1, int arg2, object arg3)
        {
            Console.WriteLine("Worker.OnPipeAlloc");

            return arg1.Libuv.buf_init(Marshal.AllocCoTaskMem(1024), 1024); //TODO: free this memory
        }

        private void OnPipeRead(
            UvStreamHandle pipe,
            int status,
            Exception error,
            object state)
        {
            Console.WriteLine("Worker.OnPipeRead");

            var httpListen = new UvTcpHandle();
            httpListen.Init(loop);
            pipe.Accept(httpListen);
            httpListen.Listen(128, _onHttpListen, "Connect");

            var httpListen1 = new UvTcpHandle();
            httpListen1.Init(loop);
            httpListen1.Bind(new System.Net.IPEndPoint(0, port));
            httpListen1.Listen(10, _onHttpListen, "Listen1");


            Console.WriteLine($"Worker on thread {Thread.CurrentThread.ManagedThreadId} started for port {port}...");

            pipe.Dispose();
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

            Interlocked.Increment(ref Connections);

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


    }
}
