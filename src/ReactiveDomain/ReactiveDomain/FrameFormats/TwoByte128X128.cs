﻿using System;
using System.Runtime.InteropServices;
using NLog;
using ReactiveDomain.Memory;
using ReactiveDomain.Util;

namespace ReactiveDomain.FrameFormats
{
    [StructLayout(LayoutKind.Sequential)]
    public unsafe struct TwoByte128X128Frame
    {
        public VideoFrameHeader FrameHeader;
        public fixed byte pixels[2 * 128 * 128];
    }
    public unsafe class TwoByte128X128Image : Image
    {
        private static readonly Logger Log = LogManager.GetLogger("Common");

        public TwoByte128X128Image(byte* buffer)
            : base(buffer, sizeof(TwoByte128X128Frame))
        {
        }

        public TwoByte128X128Image()
        {
            BufferSize = sizeof(TwoByte128X128Frame);
        }

        public static int PixelBufferLength => 2 * 128 * 128;

    }
}
