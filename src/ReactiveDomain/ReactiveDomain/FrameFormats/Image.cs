﻿using System;
using System.Runtime.InteropServices;
using System.Threading;
using NLog;
using ReactiveDomain.Memory;
using ReactiveDomain.Util;



namespace ReactiveDomain.FrameFormats
{
    //n.b. The layout of the first 4 fields for VideoFrameHeader, RawMonoFrame and RawColorFrame must match
    [StructLayout(LayoutKind.Sequential)]
    public unsafe struct VideoFrameHeader
    {
        public long FrameNumber;
        public fixed byte FrameId[16];
        public fixed byte VideoId[16];
        public double OffsetMilliseconds;
    }
    
    /// <summary>
    /// n.b. the Frame does not own the memory in the buffer
    /// the byte* passed in must be pinned and managed outside 
    /// the frame. Generally this memory will be owned by an 
    /// instance of the RawFrameBuffer or VideoWriter class.
    /// </summary>
    public abstract unsafe class Image : IDisposable
    {
        private static readonly Logger Log = NLog.LogManager.GetLogger("Common");
        private bool _disposed;
        private byte* _buffer;
        public byte* Buffer
        {
            get { CheckLifetime(); return _buffer; }
            private set { _buffer = value; }
        }
        /// <summary>
        /// This is the size of the entire frame, including the VideoFrameHeader, not just the image pixel buffer
        /// </summary>
        public int BufferSize { get; protected set; }


        private const long True = 1;
        private const long False = 0;
        private long _bufferAdded = False;
        protected Image(byte* buffer, int bufferSize)
        {
            Ensure.Positive((long)buffer, "buffer");//Null ptr test
            Ensure.Positive(bufferSize, "bufferSize");
            _buffer = buffer;
            BufferSize = bufferSize;
            Interlocked.Exchange(ref _bufferAdded, True);
        }

        //n.b this should only be be called by generic or refection constructors
        protected Image()
        {
        }

        //n.b. this should only be called once and only when using the parameterless ctor
        public void SetBuffer(byte* buffer, int bufferSize)
        {

            if (Interlocked.CompareExchange(ref _bufferAdded, True, False) != False)
            {
                Log.Error("Buffer can only be set once!");
                throw new InvalidOperationException("Buffer can only be set once!");
            }

            Ensure.Equal(BufferSize, bufferSize, "bufferSize");
            Ensure.Positive((long)buffer, "buffer");//Null ptr test
            _buffer = buffer;
           
        }

        /// <summary>
        /// This pointer points to the first pixel in the buffer
        /// </summary>
        public virtual byte* PixelBuffer
        {
            get
            {
                CheckLifetime();
                return Buffer + sizeof(VideoFrameHeader);
            }
        }

        public virtual Guid VideoId
        { 
            get
            {
                CheckLifetime();
                return Utility.ParseGuidBuffer(((VideoFrameHeader*)Buffer)->VideoId);
            }
            set
            {
                CheckLifetime();
                if (value == Guid.Empty)
                {
                    Log.Error("VideoId cannot be an empty Guid.");
                    throw new ArgumentException("VideoId cannot be an empty Guid.");
                }

                value.CopyToBuffer(((VideoFrameHeader*) Buffer)->VideoId);
            }
        }

        public virtual Guid FrameId
        {
            get
            {
                CheckLifetime();
                return Utility.ParseGuidBuffer(((VideoFrameHeader*)Buffer)->FrameId);
            }
            set
            {
                CheckLifetime();
                if (value == Guid.Empty)
                {
                    Log.Error("FrameId cannot be an empty Guid.");
                    throw new ArgumentException("FrameId cannot be an empty Guid.");
                }
                value.CopyToBuffer(((VideoFrameHeader*)Buffer)->FrameId);
            }
        }

        public virtual long FrameNumber
        {
            get
            {
                CheckLifetime();
                return ((VideoFrameHeader*)Buffer)->FrameNumber;
            }
            set
            {
                CheckLifetime();
                Ensure.Positive(value, "FrameNumber");
                ((VideoFrameHeader*)Buffer)->FrameNumber = value;
            }
        }

        public virtual double Offset
        {
            get
            {
                CheckLifetime();
                return ((VideoFrameHeader*)Buffer)->OffsetMilliseconds;
            }
            set
            {
                CheckLifetime();
                if (value < 0)
                {
                    Log.Error("Offset must be a positive value.");
                    throw new ArgumentException("Offset must be a positive value.");
                }
                ((VideoFrameHeader*)Buffer)->OffsetMilliseconds = value;
            }
        }

        public VideoFrameHeader Header => *(VideoFrameHeader*) _buffer;

        public void CopyFrom(Image other)
        {
            CheckLifetime();
            Ensure.Equal(BufferSize, other.BufferSize, "other");
            Utility.CopyMemory(Buffer, other.Buffer, (uint)BufferSize);
        }
        public void Clear()
        {
            CheckLifetime();
            Utility.Clear((IntPtr)Buffer, sizeof(ThreeByte1024X1024Frame));
        }
        #region IDisposable
        public void Dispose()
        {
            DisposeInternal();
            GC.SuppressFinalize(this);
        }

        ~Image()
        {
            DisposeInternal();
        }

        private void DisposeInternal()
        {
            if (_disposed) return;
            Buffer = (byte*)IntPtr.Zero;
            _disposed = true;
        }

        protected void CheckLifetime()
        {
            if (Interlocked.Read(ref _bufferAdded) != True)
            {
                Log.Error("No buffer has been added!");
                throw new InvalidOperationException("No buffer has been added!");
            }
            if (_disposed)
            {
                Log.Error("Object has been disposed.");
                throw new ObjectDisposedException("Object has been disposed.");
            }
        }
        #endregion
    }
}
