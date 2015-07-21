using Microsoft.AspNet.Server.Kestrel.Networking;

namespace Microsoft.AspNet.Server.Kestrel.Infrastructure
{
    public class UvWriteReqPool : ObjectPool<UvWriteReq>
    {
        private readonly UvLoopHandle _loop;

        public UvWriteReqPool(UvLoopHandle loop)
            : base (maxPooledObjects: 64)
        {
            _loop = loop;
        }

        protected override UvWriteReq Create()
        {
            var writeReq = new UvWriteReq();
            writeReq.Init(_loop);
            return writeReq;
        }
    }
}
