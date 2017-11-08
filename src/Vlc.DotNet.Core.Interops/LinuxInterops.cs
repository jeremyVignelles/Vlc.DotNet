using System;
using System.Runtime.InteropServices;

namespace Vlc.DotNet.Core.Interops
{
    using System.Text;

    public static class LinuxInterop
    {
        [DllImport("libdl", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr dlsym(IntPtr handle, string symbol);

        [DllImport("libdl", CallingConvention = CallingConvention.Cdecl)]
        public static extern bool dlclose(IntPtr handle);

        public const int RTLD_LAZY = 1;
        [DllImport("libdl", CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr dlopen(string lpFileName, int flags);
        
        [DllImport("libdl", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.Cdecl)]
        public static extern string dlerror();


        /// <summary>
        /// Format a string using printf style markers
        /// </summary>
        /// <remarks>
        /// See https://stackoverflow.com/a/37629480/2663813
        /// </remarks>
        /// <param name="buffer">The output buffer (should be large enough, use _vscprintf)</param>
        /// <param name="format">The message format</param>
        /// <param name="args">The variable arguments list pointer. We do not know what it is, but the pointer must be given as-is from C back to sprintf.</param>
        /// <returns>A negative value on failure, the number of characters written otherwise.</returns>
        [DllImport("libc", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.Cdecl)]
        public static extern int vsprintf(
            IntPtr buffer,
            string format,
            IntPtr args);

        /// <summary>
        /// Compute the size required by vsprintf to print the parameters.
        /// </summary>
        /// <param name="format"></param>
        /// <param name="ptr"></param>
        /// <returns></returns>
        [DllImport("libc", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.Cdecl)]
        public static extern int vsnprintf(
            IntPtr buffer,
            UIntPtr size,
            string format,
            IntPtr ptr);
    }
}
