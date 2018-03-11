using System;
using System.Runtime.InteropServices;

namespace LibSpeedLoad.Core.Utils
{
    public static class LZMA
    {
        private static class Win32
        {
            [DllImport("LZMA.dll", CharSet = CharSet.None, ExactSpelling = false)]
            public static extern int LzmaUncompress(byte[] dest, ref IntPtr destLen, byte[] src, ref IntPtr srcLen,
                byte[] outProps, IntPtr outPropsSize);

            [DllImport("LZMA.dll", CharSet = CharSet.None, ExactSpelling = false)]
            public static extern int LzmaUncompressBuf2File(string destFile, ref IntPtr destLen, byte[] src,
                ref IntPtr srcLen, byte[] outProps, IntPtr outPropsSize);
        }

        /**
         * EasyLZMA is used for unix systems, for Win32 we can fall back to the original LZMA.dll for now.
         */
        private static class Unix
        {
            [DllImport("EasyLZMA", CharSet = CharSet.None, ExactSpelling = false)]
            public static extern int LzmaUncompress(byte[] dest, ref IntPtr destLen, byte[] src, ref IntPtr srcLen,
                byte[] outProps, IntPtr outPropsSize);
        }

        public static int AutoUncompress(ref byte[] dest, ref IntPtr destLen, byte[] src, ref IntPtr srcLen,
            byte[] outProps, IntPtr outPropsSize)
        {
            return DebugUtil.IsLinux ? 
                Unix.LzmaUncompress(dest, ref destLen, src, ref srcLen, outProps, outPropsSize) : 
                Win32.LzmaUncompress(dest, ref destLen, src, ref srcLen, outProps, outPropsSize);
        }
    }
}