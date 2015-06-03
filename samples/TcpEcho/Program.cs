using Microsoft.AspNet.Server.Kestrel.Networking;
using Microsoft.Framework.Runtime;
using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

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

        static readonly string responseStr = "HTTP/1.1 200 OK\r\n" +
            "Content-Type: text/plain;charset=UTF-8\r\n" +
            "Content-Length: 10\r\n" +
            "Connection: keep-alive\r\n" +
            "Server: Dummy\r\n" +
            "\r\n" +
            "HelloWorld";


        private static byte[] _responseBytes = Encoding.UTF8.GetBytes(responseStr);
        private static IntPtr _responseBuffer;


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

        public void Main(string[] args)
        {
            loop.Init(uv);

            var work = new UvAsyncHandle();
            work.Init(loop, () =>
            {
                var tcpListen = new UvTcpHandle();
                tcpListen.Init(loop);
                tcpListen.Bind(new System.Net.IPEndPoint(0, 5000));
                tcpListen.Listen(10, _onTcpListen, null);

                var httpListen = new UvTcpHandle();
                httpListen.Init(loop);
                httpListen.Bind(new System.Net.IPEndPoint(0, 5001));
                httpListen.Listen(10, _onHttpListen, null);
            });

            work.Send();

            loop.Run();
            work.Dispose();
            loop.Dispose();
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
