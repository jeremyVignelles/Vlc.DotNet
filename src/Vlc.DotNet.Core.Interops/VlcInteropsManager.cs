﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
#if NETSTANDARD1_3
using System.Linq;
#endif
using System.Reflection;
using System.Runtime.InteropServices;

namespace Vlc.DotNet.Core.Interops
{
    public abstract class VlcInteropsManager : IDisposable
    {
        /// <summary>
        /// Caches of the delegates that were resolved from the libvlc.
        /// These delegates are cast to the correct delegate type when a query is made
        /// </summary>
        private readonly Dictionary<string, object> myInteropDelegates = new Dictionary<string, object>();
        private IntPtr myLibGccDllHandle;
        private IntPtr myLibVlcDllHandle;
        private IntPtr myLibVlcCoreDllHandle;

        protected VlcInteropsManager(DirectoryInfo dynamicLinkLibrariesPath)
        {
            if (!dynamicLinkLibrariesPath.Exists)
                throw new DirectoryNotFoundException();

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                var libGccDllPath = Path.Combine(dynamicLinkLibrariesPath.FullName, "libgcc_s_seh-1.dll");
                if (File.Exists(libGccDllPath))
                {
                    myLibGccDllHandle = Win32Interops.LoadLibrary(libGccDllPath);
                    if (myLibGccDllHandle == IntPtr.Zero)
                        throw new Win32Exception(Marshal.GetLastWin32Error());
                }

                var libVlcCoreDllPath = Path.Combine(dynamicLinkLibrariesPath.FullName, "libvlccore.dll");
                if (!File.Exists(libVlcCoreDllPath))
                    throw new FileNotFoundException();
                myLibVlcCoreDllHandle = Win32Interops.LoadLibrary(libVlcCoreDllPath);
                if (myLibVlcCoreDllHandle == IntPtr.Zero)
                    throw new Win32Exception(Marshal.GetLastWin32Error());

                var libVlcDllPath = Path.Combine(dynamicLinkLibrariesPath.FullName, "libvlc.dll");
                if (!File.Exists(libVlcDllPath))
                    throw new FileNotFoundException();
                myLibVlcDllHandle = Win32Interops.LoadLibrary(libVlcDllPath);
                if (myLibVlcDllHandle == IntPtr.Zero)
                    throw new Win32Exception(Marshal.GetLastWin32Error());
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                var libVlcCoreDllPath = Path.Combine(dynamicLinkLibrariesPath.FullName, "libvlccore.so");
                if (!File.Exists(libVlcCoreDllPath))
                    throw new FileNotFoundException();
                myLibVlcCoreDllHandle = LinuxInterop.dlopen(libVlcCoreDllPath, 1);
                if (myLibVlcCoreDllHandle == IntPtr.Zero)
                {
                    throw new Exception(LinuxInterop.dlerror());
                }

                var libVlcDllPath = Path.Combine(dynamicLinkLibrariesPath.FullName, "libvlc.so");
                if (!File.Exists(libVlcDllPath))
                    throw new FileNotFoundException();
                myLibVlcDllHandle = LinuxInterop.dlopen(libVlcDllPath, 1);
                if (myLibVlcDllHandle == IntPtr.Zero)
                    throw new Exception(LinuxInterop.dlerror());
            }
            else
            {
                throw new PlatformNotSupportedException();
            }
        }

        internal T GetInteropDelegate<T>()
        {
            string vlcFunctionName = null;
            try
            {
#if NETSTANDARD1_3
                var attrs = typeof(T).GetTypeInfo().GetCustomAttributes(typeof(LibVlcFunctionAttribute), false).ToArray();
#else
                var attrs = typeof(T).GetCustomAttributes(typeof(LibVlcFunctionAttribute), false);
#endif
                if (attrs.Length == 0)
                    throw new Exception("Could not find the LibVlcFunctionAttribute.");
                var attr = (LibVlcFunctionAttribute)attrs[0];
                vlcFunctionName = attr.FunctionName;
                if (myInteropDelegates.ContainsKey(vlcFunctionName))
                {
                    return (T) myInteropDelegates[attr.FunctionName];
                }

                IntPtr procAddress;
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    procAddress = Win32Interops.GetProcAddress(myLibVlcDllHandle, attr.FunctionName);
                    if (procAddress == IntPtr.Zero)
                        throw new Win32Exception();
                }
                else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                {
                    procAddress = LinuxInterop.dlsym(myLibVlcDllHandle, attr.FunctionName);
                    if (procAddress == IntPtr.Zero)
                        throw new Exception(LinuxInterop.dlerror());
                }
                else
                {
                    throw new PlatformNotSupportedException();
                }

                object delegateForFunctionPointer;
#if NET20||NET35||NET40||NET45
                delegateForFunctionPointer = Marshal.GetDelegateForFunctionPointer(procAddress, typeof(T));
#else
                // The GetDelegateForFunctionPointer with two parameters is now deprecated.
                delegateForFunctionPointer = Marshal.GetDelegateForFunctionPointer<T>(procAddress);
#endif
                myInteropDelegates[attr.FunctionName] = delegateForFunctionPointer;
                return (T)delegateForFunctionPointer;
            }
            catch (Win32Exception e)
            {
                throw new MissingMethodException(string.Format("The address of the function '{0}' does not exist in libvlc library.", vlcFunctionName), e);
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        public virtual void Dispose(bool disposing)
        {
            if (myLibVlcDllHandle != IntPtr.Zero)
            {
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    Win32Interops.FreeLibrary(myLibVlcDllHandle);
                }
                else if(RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                {
                    LinuxInterop.dlclose(myLibGccDllHandle);
                }
                myLibVlcDllHandle = IntPtr.Zero;
            }
            if (myLibVlcCoreDllHandle != IntPtr.Zero)
            {
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    Win32Interops.FreeLibrary(myLibVlcCoreDllHandle);
                }
                else if(RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                {
                    LinuxInterop.dlclose(myLibVlcCoreDllHandle);
                }
                myLibVlcCoreDllHandle = IntPtr.Zero;
            }
            if (myLibGccDllHandle != IntPtr.Zero)
            {
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    Win32Interops.FreeLibrary(myLibGccDllHandle);
                }
                myLibGccDllHandle = IntPtr.Zero;
            }
        }

        ~VlcInteropsManager()
        {
            Dispose(false);
        }
    }
}
