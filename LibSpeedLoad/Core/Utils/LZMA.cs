using System;
using System.Runtime.InteropServices;

namespace LibSpeedLoad.Core.Utils
{
    public static class LZMA
    {
        [DllImport("EasyLZMA", CharSet = CharSet.None, ExactSpelling = false, CallingConvention = CallingConvention.Cdecl)]
        public static extern int LzmaUncompress(byte[] dest, ref IntPtr destLen, byte[] src, ref IntPtr srcLen,
            byte[] outProps, IntPtr outPropsSize);
    }
}