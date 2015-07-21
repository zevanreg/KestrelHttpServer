using System;
using System.Collections.Generic;

namespace Microsoft.AspNet.Server.Kestrel.Infrastructure
{
    public abstract class ObjectPool<T> : IDisposable
        where T : IDisposable
    {
        private readonly int _maxPooledObjects;

        private readonly Stack<T> _stack = new Stack<T>();
        private readonly object _sync = new object();

        protected ObjectPool(int maxPooledObjects)
        {
            _maxPooledObjects = maxPooledObjects;
        }

        public T Alloc()
        {
            lock (_sync)
            {
                if (_stack.Count != 0)
                {
                    return _stack.Pop();
                }
            }

            return Create();
        }

        public void Free(T obj)
        {
            lock (_sync)
            {
                if (_stack.Count < _maxPooledObjects)
                {
                    _stack.Push(obj);
                }
                else
                {
                    obj.Dispose();
                }
            }
        }

        public void Dispose()
        {
            lock (_sync)
            {
                foreach (var obj in _stack)
                {
                    obj.Dispose();
                }

                _stack.Clear();
            }
        }

        protected abstract T Create();
    }
}
