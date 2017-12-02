using System;
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
                        // This code is based on the research work that was made here : https://github.com/jeremyVignelles/va-list-interop-demo
                        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows) && 
                            (RuntimeInformation.ProcessArchitecture == Architecture.X64 || RuntimeInformation.ProcessArchitecture == Architecture.X86))
                        {
                            this.Manager.SetLog(this.OnLogInternal);
                        }
                        else if(RuntimeInformation.IsOSPlatform(OSPlatform.Linux) && RuntimeInformation.ProcessArchitecture == Architecture.X64)
                        {
                            this.Manager.SetLog(this.OnLogInternalLinuxX64);
                        }
                        else if(RuntimeInformation.IsOSPlatform(OSPlatform.Linux) && RuntimeInformation.ProcessArchitecture == Architecture.X64)
                        {
                            this.Manager.SetLog(this.OnLogInternalLinuxX86);
                        }
                        else
                        {
                            throw new PlatformNotSupportedException();
                        }
                        
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

        private void OnLogInternal(IntPtr data, VlcLogLevel level, IntPtr logContext, string format, IntPtr args)
        {
            if (this.log != null)
            {
                // Original source for va_list handling: https://stackoverflow.com/a/37629480/2663813
                int byteLength = Win32Interops._vscprintf(format, args) + 1;
                
                var utf8Buffer = Marshal.AllocHGlobal(byteLength);

                string formattedDecodedMessage;
                try {
                    Win32Interops.vsprintf(utf8Buffer, format, args);

                    formattedDecodedMessage = Utf8InteropStringConverter.Utf8InteropToString(utf8Buffer);
                }
                finally
                {
                    Marshal.FreeHGlobal(utf8Buffer);
                }

                this.CallLogCallback(level, logContext, formattedDecodedMessage);
            }
        }

        private void OnLogInternalLinuxX86(IntPtr data, VlcLogLevel level, IntPtr logContext, string format, IntPtr args)
        {
            if (this.log != null)
            {
                int byteLength = LinuxInterop.vsnprintf(IntPtr.Zero, UIntPtr.Zero, format, args) + 1;
                
                var utf8Buffer = Marshal.AllocHGlobal(byteLength);

                string formattedDecodedMessage;
                try {
                    LinuxInterop.vsprintf(utf8Buffer, format, args);

                    formattedDecodedMessage = Utf8InteropStringConverter.Utf8InteropToString(utf8Buffer);
                }
                finally
                {
                    Marshal.FreeHGlobal(utf8Buffer);
                }
                
                this.CallLogCallback(level, logContext, formattedDecodedMessage);
            }
        }

        private void OnLogInternalLinuxX64(IntPtr data, VlcLogLevel level, IntPtr logContext, string format, IntPtr args)
        {
            if (this.log != null)
            {
                // The args pointer cannot be reused between two calls. We need to make a copy of the underlying structure.
#if NET20 || NET35 || NET40 || NET45
            var listStructure = (VaListLinuxX64)Marshal.PtrToStructure(args, typeof(VaListLinuxX64));
#else
                var listStructure = Marshal.PtrToStructure<VaListLinuxX64>(args);
#endif
                IntPtr listPointer = Marshal.AllocHGlobal(Marshal.SizeOf(listStructure));
                int byteLength;
                try
                {
                    Marshal.StructureToPtr(listStructure, listPointer, false);
                    byteLength = LinuxInterop.vsnprintf(IntPtr.Zero, UIntPtr.Zero, format, listPointer) + 1;
                }
                finally
                {
                    Marshal.FreeHGlobal(listPointer);
                }
                
                var utf8Buffer = Marshal.AllocHGlobal(byteLength);
                string formattedDecodedMessage;
                try
                {
                    listPointer = Marshal.AllocHGlobal(Marshal.SizeOf(listStructure));
                    try
                    {
                        Marshal.StructureToPtr(listStructure, listPointer, false);
                        LinuxInterop.vsprintf(utf8Buffer, format, listPointer);
                        formattedDecodedMessage = Utf8InteropStringConverter.Utf8InteropToString(utf8Buffer);
                    }
                    finally
                    {
                        Marshal.FreeHGlobal(listPointer);
                    }
                }
                finally
                {
                    Marshal.FreeHGlobal(utf8Buffer);
                }
                
                this.CallLogCallback(level, logContext, formattedDecodedMessage);
            }
        }
        
        /// <summary>
        /// The function that resolves the log context and calls the callback
        /// with the message that was decoded previously in a platform-specific way
        /// </summary>
        /// <param name="level">The log level</param>
        /// <param name="logContext">The log context that is used to fetch the message location</param>
        /// <param name="message">The message</param>
        private void CallLogCallback(VlcLogLevel level, IntPtr logContext, string message)
        {
            string module;
            string file;
            uint? line;
            this.Manager.GetLogContext(logContext, out module, out file, out line);

            var logEventArgs = new VlcMediaPlayerLogEventArgs(level, message, module, file, line);
            // Do the notification on another thread, so that VLC is not interrupted by the logging
#if NETSTANDARD1_3
            Task.Run(() => this.log(this.myMediaPlayerInstance, logEventArgs));
#else
            ThreadPool.QueueUserWorkItem(eventArgs =>
            {
                this.log(this.myMediaPlayerInstance, (VlcMediaPlayerLogEventArgs)eventArgs);
            }, logEventArgs);
#endif
        }
    }
}