using System;
using System.Runtime.InteropServices;
using NLog;
using ReactiveDomain.Memory;
using ReactiveDomain.Util;

namespace ReactiveDomain.FrameFormats
{
    [StructLayout(LayoutKind.Sequential)]
    public unsafe struct ThreeByte1024X1024Frame
    {
        public VideoFrameHeader FrameHeader;
        public fixed byte pixels[3*1024*1024];
    }
    public unsafe class ThreeByte1024X1024Image : Image
    {
        private static readonly Logger Log = LogManager.GetLogger("Common");

        public const int PixelHeight = 1024;
        public const int PixelWidth = 1024;
        public const int Rgb24Bytes = 3;

        public ThreeByte1024X1024Image(byte* buffer)
            : base(buffer, sizeof(ThreeByte1024X1024Frame))
        {

        }

        public override byte* PixelBuffer
        {
            get
            {
                CheckLifetime();
                return ((ThreeByte1024X1024Frame*)Buffer)->pixels;
            }
        }

        public static int PixelBufferLength => 3 * 1024 * 1024;

    }
}