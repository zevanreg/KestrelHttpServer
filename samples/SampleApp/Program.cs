// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Microsoft.AspNet.Server.Kestrel.Networking;
using Microsoft.Framework.Runtime;
using System;
using System.Diagnostics;
using System.Threading;

namespace SampleApp
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
            
            var w = new Worker((ILibraryManager)_services.GetService(typeof(ILibraryManager)));
            w.Start();

            WaitCallback threadPoolCallback = _ => { };
            var ticks = 0;
            Action postCallback = () =>
            {
                ThreadPool.QueueUserWorkItem(threadPoolCallback);
                //ThreadPool.UnsafeQueueUserWorkItem(threadPoolCallback, null);
            };

            threadPoolCallback = _ =>
            {
                w.Post(postCallback);
                Interlocked.Increment(ref ticks);
            };

            for (var x = 0; x != 1000; ++x)
            {
                w.Post(postCallback);
            }

            var thread = new Thread(() =>
            {
                var sw = new Stopwatch();
                sw.Start();
                for (; ;)
                {
                    Thread.Sleep(TimeSpan.FromSeconds(1));
                    Console.WriteLine("{0} {1}/sec", ticks, ticks / sw.Elapsed.TotalSeconds);
                }
            });
            thread.Start();
            
            new Microsoft.AspNet.Hosting.Program(_services).Main(new[] {
                "--server","Kestrel"
            });
        }
    }
}
