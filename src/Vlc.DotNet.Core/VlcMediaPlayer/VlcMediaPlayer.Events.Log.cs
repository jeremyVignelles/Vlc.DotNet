﻿using System;
using Vlc.DotNet.Core.Interops.Signatures;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
#if NETSTANDARD1_3
using System.Threading.Tasks;
#endif
using Vlc.DotNet.Core.Interops;

namespace Vlc.DotNet.Core
{
    public sealed partial class VlcMediaPlayer
    {
        private object _logLock = new object();

        /// <summary>
        /// The real log event handlers.
        /// </summary>
        private EventHandler<VlcMediaPlayerLogEventArgs> log;

        /// <summary>
        /// A boolean to make sure that we are calling SetLog only once
        /// </summary>
        private bool logAttached = false;

        /// <summary>
        /// The event that is triggered when a log is emitted from libVLC.
        /// Listening to this event will discard the default logger in libvlc.
        /// </summary>
        public event EventHandler<VlcMediaPlayerLogEventArgs> Log
        {
            add
            {
                lock (this._logLock)
                {
                    this.log += value;
                    if (!this.logAttached)
                    {
                        this.Manager.SetLog(this.OnLogInternal);
                        this.logAttached = true;
                    }
                }
            }

            remove
            {
                lock (this._logLock)
                {
                    this.log -= value;
                }
            }
        }

        private void OnLogInternal(IntPtr data, VlcLogLevel level, IntPtr ctx, string format, IntPtr args)
        {
            if (this.log != null)
            {
                // Original source for va_list handling: https://stackoverflow.com/a/37629480/2663813
                int byteLength;
                if(RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    byteLength = Win32Interops._vscprintf(format, args) + 1;
                }
                else if(RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                {
                    byteLength = LinuxInterop.vsnprintf(IntPtr.Zero, UIntPtr.Zero, format, args) + 1;
                }
                else
                {
                    throw new PlatformNotSupportedException();
                }
                
                var utf8Buffer = Marshal.AllocHGlobal(byteLength);

                string formattedDecodedMessage;
                try {
                    if(RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                    {
                        Win32Interops.vsprintf(utf8Buffer, format, args);
                    }
                    else if(RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                    {
                        LinuxInterop.vsprintf(utf8Buffer, format, args);
                    }

                    formattedDecodedMessage = Utf8InteropStringConverter.Utf8InteropToString(utf8Buffer);
                }
                finally
                {
                    Marshal.FreeHGlobal(utf8Buffer);
                }

                string module;
                string file;
                uint? line;
                this.Manager.GetLogContext(ctx, out module, out file, out line);

                // Do the notification on another thread, so that VLC is not interrupted by the logging
#if NETSTANDARD1_3
                Task.Run(() => this.log(this.myMediaPlayerInstance, new VlcMediaPlayerLogEventArgs(level, formattedDecodedMessage, module, file, line)));
#else
                ThreadPool.QueueUserWorkItem(eventArgs =>
                {
                    this.log(this.myMediaPlayerInstance, (VlcMediaPlayerLogEventArgs)eventArgs);
                }, new VlcMediaPlayerLogEventArgs(level, formattedDecodedMessage, module, file, line));
#endif
            }
        }
    }
}