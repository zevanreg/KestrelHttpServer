// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Server.Kestrel;
using Microsoft.AspNetCore.Server.Kestrel.Filter;
using Microsoft.AspNetCore.Server.Kestrel.Http;
using Microsoft.AspNetCore.Server.Kestrel.Infrastructure;
using Microsoft.Extensions.Configuration;

namespace Microsoft.AspNetCore.Server.KestrelTests
{
    public class TestServiceContext : ServiceContext
    {
        private RequestDelegate _app;

        public TestServiceContext()
        {
            AppLifetime = new LifetimeNotImplemented();
            Log = new TestKestrelTrace();
            ThreadPool = new LoggingThreadPool(Log);
            DateHeaderValueManager = new TestDateHeaderValueManager();

            var configuration = new ConfigurationBuilder().Build();
            ServerInformation = new KestrelServerInformation(configuration);
            ServerInformation.ShutdownTimeout = TimeSpan.FromSeconds(5);

            HttpComponentFactory = new HttpComponentFactory(ServerInformation);
        }

        public TestServiceContext(IConnectionFilter filter)
            : this()
        {
            ServerInformation.ConnectionFilter = filter;
        }

        public RequestDelegate App
        {
            get
            {
                return _app;
            }
            set
            {
                _app = value;
                FrameFactory = connectionContext =>
                {
                    return new Frame<HttpContext>(new DummyApplication(_app), connectionContext);
                };
            }
        }
    }
}
