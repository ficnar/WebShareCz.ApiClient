using System;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;

namespace MaFi.WebShareCz.ApiClient.Http
{
    internal sealed class TransportStreamContent : StreamContent
    {
        private readonly TransportStream _sourceTransportStream;

        public TransportStreamContent(TransportStream sourceTransportStream, string fileName, int bufferSize) : base(sourceTransportStream, bufferSize)
        {
            _sourceTransportStream = sourceTransportStream;
            Headers.ContentDisposition = new ContentDispositionHeaderValue("form-data")
            {
                Name = "file",
                FileName = fileName,
            };
        }

        protected override bool TryComputeLength(out long length)
        {
            if (_sourceTransportStream.TryComputeLength(out length))
                return true;
            length = -1;
            return false;
        }
    }
}
