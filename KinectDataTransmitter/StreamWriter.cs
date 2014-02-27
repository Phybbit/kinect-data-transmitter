using System;
using System.Collections.Generic;
using System.IO.MemoryMappedFiles;
using System.Linq;
using System.Text;
using DataConverter;
using Microsoft.Kinect;

namespace KinectDataTransmitter
{
    public class StreamWriter : IDisposable
    {
        private bool _disposed;
        private MemoryMappedFile _colorMemoryMappedFile;
        private MemoryMappedFile _depthMemoryMappedFile;
        private MemoryMappedFile _infraredMemoryMappedFile;

        public StreamWriter()
        {
            _colorMemoryMappedFile = MemoryMappedFile.CreateOrOpen(Converter.ColorStreamFileName, Converter.ColorStreamFileSize, MemoryMappedFileAccess.ReadWrite);
            _depthMemoryMappedFile = MemoryMappedFile.CreateOrOpen(Converter.DepthStreamFileName, Converter.DepthStreamFileSize, MemoryMappedFileAccess.ReadWrite);
            _infraredMemoryMappedFile = MemoryMappedFile.CreateOrOpen(Converter.InfraRedStreamFileName, Converter.InfraRedStreamFileSize, MemoryMappedFileAccess.ReadWrite);
        }


        ~StreamWriter()
        {
            Dispose(false);
        }


        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (_colorMemoryMappedFile != null)
                {
                    _colorMemoryMappedFile.Dispose();
                }
                if (_depthMemoryMappedFile != null)
                {
                    _depthMemoryMappedFile.Dispose();
                }
                if (_infraredMemoryMappedFile != null)
                {
                    _infraredMemoryMappedFile.Dispose();
                }
                _disposed = true;
            }
        }

            
        public void ProcessColorData(byte[] colorImageData)
        {
            using (var accessor = _colorMemoryMappedFile.CreateViewAccessor(0, colorImageData.Length, MemoryMappedFileAccess.Write))
            {
                accessor.WriteArray(0, colorImageData, 0, colorImageData.Length);
            }

            Console.WriteLine(Converter.VideoFrameType);
        }

        public void ProcessDepthData(short[] depthImageData)
        {
            const int bytesPerShort = 2;
            using (var accessor = _depthMemoryMappedFile.CreateViewAccessor(0, depthImageData.Length * bytesPerShort, MemoryMappedFileAccess.Write))
            {
                accessor.WriteArray(0, depthImageData, 0, depthImageData.Length);
            }

            Console.WriteLine(Converter.DepthFrameType);
        }

        public void ProcessInfraRedData(byte[] data)
        {
            using (var accessor = _infraredMemoryMappedFile.CreateViewAccessor(0, data.Length, MemoryMappedFileAccess.Write))
            {
                accessor.WriteArray(0, data, 0, data.Length);
            }

            Console.WriteLine(Converter.InfraRedFrameType);
        }
    }
}
