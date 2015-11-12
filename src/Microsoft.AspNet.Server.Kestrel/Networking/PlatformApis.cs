// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Runtime.InteropServices;

namespace Microsoft.AspNet.Server.Kestrel.Networking
{
    public static class PlatformApis
    {
        private static bool? _isWindows;
        private static bool? _isDarwin;

        public static bool IsWindows()
        {
            if (!_isWindows.HasValue)
            {
#if DOTNET5_4 || DNXCORE50
                // Until Environment.OSVersion.Platform is exposed on .NET Core, we
                // try to call uname and if that fails we assume we are on Windows.
                _isWindows = GetUname() == string.Empty;
#else
                var p = (int)Environment.OSVersion.Platform;
                _isWindows = (p != 4) && (p != 6) && (p != 128);
#endif
            }

            return _isWindows.Value;
        }

        public static bool IsDarwin()
        {
            if (!_isDarwin.HasValue)
            {
                _isDarwin = string.Equals(GetUname(), "Darwin", StringComparison.Ordinal);
            }

            return _isDarwin.Value;
        }

        [DllImport("libc")]
        static extern int uname(IntPtr buf);

        static unsafe string GetUname()
        {
            var buffer = new byte[8192];
            try
            {
                fixed (byte* buf = buffer)
                {
                    if (uname((IntPtr)buf) == 0)
                    {
                        return Marshal.PtrToStringAnsi((IntPtr)buf);
                    }
                }
                return string.Empty;
            }
            catch
            {
                return string.Empty;
            }
        }
    }
}
