using System;
using System.IO;

namespace MaFi.WebShareCz.ApiClient.Http
{
    internal sealed class TransportStream : Stream
    {
        private readonly Stream _originalReadStream;
        private readonly long _originalLength;
        private readonly IProgress<int> _progress;
        private long? _targetLength = null;
        private int _progressScale = 0;

        long bytesTransferred = 0;
        int donePercent = -1;

        public TransportStream(Stream originalReadStream, long length, IProgress<int> progress)
        {
            _originalReadStream = originalReadStream;
            _originalLength = length;
            _progress = progress;
        }

        public bool TryComputeLength(out long length)
        {
            EnsureComputedLength();
            length = _targetLength.Value;
            return _targetLength != -1;
        }

        private void EnsureComputedLength()
        {
            if (_targetLength == null)
            {
                if (_originalLength >= 0)
                {
                    _targetLength = _originalLength;
                }
                else
                    _targetLength = -1;
            }
        }

        public override bool CanRead => true;

        public override bool CanSeek => false;

        public override bool CanWrite => false;

        public override long Length 
        {
            get
            {
                if (_targetLength == null)
                {
                    TryComputeLength(out long targetLength);
                    _targetLength = targetLength;
                }
                return _targetLength.Value != -1 ? _targetLength.Value : throw new NotSupportedException();
            }
        }

        public override long Position { get => _originalReadStream.Position; set => throw new NotSupportedException(); }

        public override void Flush()
        {
            throw new NotSupportedException();
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            int bytesReaded = _originalReadStream.Read(buffer, offset, count);
            if (_progress != null)
            {
                EnsureComputedLength();
                if (_targetLength > 0)
                {
                    if (_progressScale == 0)
                    {
                        long wantedBuffers = _targetLength.Value / count;
                        _progressScale = Math.Max(1, (int)(wantedBuffers / 2000));
                    }
                    bytesTransferred += bytesReaded;
                    int percent = decimal.ToInt32((bytesTransferred * 100 * _progressScale) / (decimal)_targetLength);
                    if (percent != donePercent)
                    {
                        donePercent = percent;
                        _progress.Report(percent / _progressScale);
                    }
                }
            }
            return bytesReaded;
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            throw new NotSupportedException();
        }

        public override void SetLength(long value)
        {
            throw new NotSupportedException();
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            throw new NotSupportedException();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
                _originalReadStream.Dispose();
            base.Dispose(disposing);
        }
    }
}
