// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using Microsoft.Extensions.Logging;

namespace Microsoft.AspNet.Server.Kestrel
{
    /// <summary>
    /// Summary description for KestrelTrace
    /// </summary>
    public class KestrelTrace : ILogger
    {
        private const int EventID_ConnectionStart = 1;
        private const int EventID_ConnectionStop = 2;
        private const int EventID_ConnectionRead = 3;
        private const int EventID_ConnectionPause = 4;
        private const int EventID_ConnectionResume = 5;
        private const int EventID_ConnectionReadFin = 6;
        private const int EventID_ConnectionWriteFin = 7;
        private const int EventID_ConnectionWroteFin = 8;
        private const int EventID_ConnectionKeepAlive = 9;
        private const int EventID_ConnectionDisconnect = 10;
        private const int EventID_ConnectionWrite = 11;
        private const int EventID_ConnectionWriteCallback = 12;
        private const int EventID_ApplicationError = 13;

        protected readonly ILogger _logger;

        public KestrelTrace(ILogger logger)
        {
            _logger = logger;
        }

        public virtual void ConnectionStart(long connectionId)
        {
            LogDebugMessage(EventID_ConnectionStart, $"Connection [{connectionId}] started.");
        }

        public virtual void ConnectionStop(long connectionId)
        {
            LogDebugMessage(EventID_ConnectionStop, $"Connection [{connectionId}] stopped.");
        }

        public virtual void ConnectionRead(long connectionId, int count)
        {
            // Don't log for now since this could be *too* verbose.
            // Reserved: Event ID 3
            // LogDebugMessage(EventID_ConnectionRead, $"Connection [{connectionId}] read.");
        }

        public virtual void ConnectionPause(long connectionId)
        {
            LogDebugMessage(EventID_ConnectionPause, $"Connection [{connectionId}] paused.");
        }

        public virtual void ConnectionResume(long connectionId)
        {
            LogDebugMessage(EventID_ConnectionResume, $"Connection [{connectionId}] resumed.");
        }

        public virtual void ConnectionReadFin(long connectionId)
        {
            LogDebugMessage(EventID_ConnectionReadFin, $"Connection [{connectionId}] received FIN.");
        }

        public virtual void ConnectionWriteFin(long connectionId)
        {
            LogDebugMessage(EventID_ConnectionWriteFin, $"Connection [{connectionId}] sending FIN.");
        }

        public virtual void ConnectionWroteFin(long connectionId, int status)
        {
            LogDebugMessage(EventID_ConnectionWroteFin, $"Connection [{connectionId}] sent FIN with status [{status}].");
        }

        public virtual void ConnectionKeepAlive(long connectionId)
        {
            LogDebugMessage(EventID_ConnectionKeepAlive, $"Connection [{connectionId}] completed keep alive response.");
        }

        public virtual void ConnectionDisconnect(long connectionId)
        {
            LogDebugMessage(EventID_ConnectionDisconnect, $"Connection [{connectionId}] disconnected.");
        }

        public virtual void ConnectionWrite(long connectionId, int count)
        {
            // Don't log for now since this could be *too* verbose.
            // Reserved: Event ID 11
        }

        public virtual void ConnectionWriteCallback(long connectionId, int status)
        {
            // Don't log for now since this could be *too* verbose.
            // Reserved: Event ID 12
        }

        public virtual void ApplicationError(Exception ex)
        {
            _logger.LogError(13, "An unhandled exception was thrown by the application.", ex);
        }

        public virtual void Log(LogLevel logLevel, int eventId, object state, Exception exception, Func<object, Exception, string> formatter)
        {
            _logger.Log(logLevel, eventId, state, exception, formatter);
        }

        public virtual bool IsEnabled(LogLevel logLevel)
        {
            return _logger.IsEnabled(logLevel);
        }

        public virtual IDisposable BeginScopeImpl(object state)
        {
            return _logger.BeginScopeImpl(state);
        }

        protected virtual void LogDebugMessage(int eventId, string message)
        {
            _logger?.LogDebug(eventId, message);
        }
    }
}