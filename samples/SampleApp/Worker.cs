using Microsoft.AspNet.Server.Kestrel;
using Microsoft.AspNet.Server.Kestrel.Networking;
using Microsoft.Framework.Runtime;
using System;
using System.Collections.Generic;
using System.Threading;

namespace SampleApp
{
    internal class Worker
    {
        readonly Thread _thread;
        readonly KestrelEngine _engine;
        readonly Libuv _uv;
        readonly UvLoopHandle _loop;
        readonly UvAsyncHandle _async;
        readonly List<Action> _actions;

        public Worker(ILibraryManager lm)
        {
            _thread = new Thread(ThreadStart);
            _engine = new KestrelEngine(lm);
            _uv = _engine.Libuv;
            _loop = new UvLoopHandle();
            _async = new UvAsyncHandle();
            _actions = new List<Action>();
        }

        public void Start()
        {
            var wait = new ManualResetEvent(false);
            _thread.Start(wait);
            wait.WaitOne();
        }

        private void ThreadStart(object w)
        {
            _loop.Init(_uv);
            _async.Init(_loop, OnAsync);
            ((ManualResetEvent)w).Set();
            _loop.Run();
        }

        public void Post(Action action)
        {
            lock (_actions)
            {
                _actions.Add(action);
            }
            _async.Send();
        }

        private void OnAsync()
        {
            List<Action> actions;
            lock (_actions)
            {
                actions = new List<Action>(_actions);
                _actions.Clear();
            }
            foreach(var action in actions)
            {
                action.Invoke();
            }
        }
    }
}

