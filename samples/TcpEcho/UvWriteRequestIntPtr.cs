using Microsoft.AspNet.Server.Kestrel.Networking;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace TcpEcho
{
    public class UvWriteReqNative : UvReq
    {
        private readonly static Libuv.uv_write_cb _uv_write_cb = UvWriteCb;

        IntPtr _bufs;

        Action<UvWriteReqNative, int, Exception, object> _callback;
        object _state;

        GCHandle _pin;

        public void Init(UvLoopHandle loop)
        {
            var requestSize = loop.Libuv.req_size(Libuv.RequestType.WRITE);
            var bufferSize = Marshal.SizeOf<Libuv.uv_buf_t>();
            CreateMemory(
                loop.Libuv,
                loop.ThreadId,
                requestSize + bufferSize);
            _bufs = handle + requestSize;
        }

        public unsafe void Write(
            UvStreamHandle handle,
            IntPtr memory,
            int length,
            Action<UvWriteReqNative, int, Exception, object> callback,
            object state)
        {
            try
            {
                // add GCHandle to keeps this SafeHandle alive while request processing
                _pin = GCHandle.Alloc(this, GCHandleType.Normal);

                var pBuffers = (Libuv.uv_buf_t*)_bufs;
                pBuffers[0] = Libuv.buf_init(
                    memory,
                    length);

                _callback = callback;
                _state = state;
                _uv.write(this, handle, pBuffers, 1, _uv_write_cb);
            }
            catch
            {
                _callback = null;
                _state = null;
                _pin.Free();
                throw;
            }
        }

        private static void UvWriteCb(IntPtr ptr, int status)
        {
            var req = FromIntPtr<UvWriteReqNative>(ptr);
            req._pin.Free();

            var callback = req._callback;
            req._callback = null;

            var state = req._state;
            req._state = null;

            Exception error = null;
            if (status < 0)
            {
                req.Libuv.Check(status, out error);
            }

            try
            {
                callback(req, status, error, state);
            }
            catch (Exception ex)
            {
                Trace.WriteLine("UvWriteCb " + ex.ToString());
            }
        }
    }

}
