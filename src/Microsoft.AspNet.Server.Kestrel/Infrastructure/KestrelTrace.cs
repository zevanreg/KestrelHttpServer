// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Text;
using Microsoft.AspNet.Http.Features;
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

        private const int EventID_BeginProcessingRequest = 14;
        private const int EventID_FinishProcessingRequest = 15;
        private const int EventID_FailProcessingRequest = 16;

        protected readonly ILogger _logger;

        public KestrelTrace(ILogger logger)
        {
            _logger = logger;
        }

        public virtual void ConnectionStart(long connectionId)
        {
            LogMessageForSocket(EventID_ConnectionStart, connectionId, "started");
        }

        public virtual void ConnectionStop(long connectionId)
        {
            LogMessageForSocket(EventID_ConnectionStop, connectionId, "stopped");
        }

        public virtual void ConnectionRead(long connectionId, int count)
        {
            // Don't log for now since this could be *too* verbose.
            // Reserved: Event ID 3
        }

        public virtual void ConnectionPause(long connectionId)
        {
            LogMessageForSocket(EventID_ConnectionPause, connectionId, "paused");
        }

        public virtual void ConnectionResume(long connectionId)
        {
            LogMessageForSocket(EventID_ConnectionResume, connectionId, "resumed");
        }

        public virtual void ConnectionReadFin(long connectionId)
        {
            LogMessageForSocket(EventID_ConnectionReadFin, connectionId, "received FIN");
        }

        public virtual void ConnectionWriteFin(long connectionId)
        {
            LogMessageForSocket(EventID_ConnectionWriteFin, connectionId, "sending FIN");
        }

        public virtual void ConnectionWroteFin(long connectionId, int status)
        {
            LogMessageForSocket(EventID_ConnectionWroteFin, connectionId, $"sent FIN with status [{status}]");
        }

        public virtual void ConnectionKeepAlive(long connectionId)
        {
            LogMessageForSocket(EventID_ConnectionKeepAlive, connectionId, "completed and keep alive response");
        }

        public virtual void ConnectionDisconnect(long connectionId)
        {
            LogMessageForSocket(EventID_ConnectionDisconnect, connectionId, "disconnected");
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
            _logger.LogError(
                EventID_ApplicationError,
                "An unhandled exception was thrown by the application.",
                ex);
        }

        public virtual void LogRequest(IHttpRequestFeature request)
        {
            if (_logger.IsEnabled(LogLevel.Verbose))
            {
                var strBuilder = new StringBuilder();
                strBuilder.AppendLine($"[HTTP] Request {request.Method} {request.PathBase}{request.Path}{request.QueryString} {request.Protocol}");
                strBuilder.AppendLine("Headers:");

                foreach (var header in request.Headers)
                {
                    strBuilder.AppendLine($"  {header.Key}: {header.Value}");
                }

                _logger.LogDebug(EventID_BeginProcessingRequest, strBuilder.ToString());
            }
            else if (_logger.IsEnabled(LogLevel.Information))
            {
                _logger.LogInformation(EventID_BeginProcessingRequest, $"[HTTP] Request {request.Method} {request.Path} {request.Protocol}");
            }
        }

        public virtual void LogResponse(IHttpResponseFeature response)
        {
            if (_logger.IsEnabled(LogLevel.Verbose))
            {
                var strBuilder = new StringBuilder();
                strBuilder.AppendLine($"[HTTP] Response {response.StatusCode}");
                strBuilder.AppendLine("Headers:");

                foreach (var header in response.Headers)
                {
                    strBuilder.AppendLine($"  {header.Key}: {header.Value}");
                }

                _logger.LogDebug(EventID_FinishProcessingRequest, strBuilder.ToString());
            }
            else if (_logger.IsEnabled(LogLevel.Information))
            {
                _logger.LogInformation(EventID_FinishProcessingRequest, $"[HTTP] Response {response.StatusCode}");
            }
        }

        public virtual void FailProcessingRequest(IHttpRequestFeature request, Exception ex)
        {
            _logger.LogError(EventID_FailProcessingRequest, "[HTTP] Fail receiving request");
        }

        void ILogger.Log(LogLevel logLevel, int eventId, object state, Exception exception, Func<object, Exception, string> formatter)
        {
            _logger.Log(logLevel, eventId, state, exception, formatter);
        }

        bool ILogger.IsEnabled(LogLevel logLevel)
        {
            return _logger.IsEnabled(logLevel);
        }

        IDisposable ILogger.BeginScopeImpl(object state)
        {
            return _logger.BeginScopeImpl(state);
        }

        protected virtual void LogDebugMessage(int eventId, string message)
        {
            _logger?.LogDebug(eventId, message);
        }

        protected virtual void LogMessageForSocket(int eventId, long connectionId, string message)
        {
            _logger?.LogDebug(eventId, $"[SOCK] Connection id: {connectionId} {message}.");
        }
    }
}