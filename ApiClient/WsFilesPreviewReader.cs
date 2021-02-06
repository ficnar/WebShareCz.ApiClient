using System;
using System.Collections;
using System.Collections.Generic;
using MaFi.WebShareCz.ApiClient.Entities;

namespace MaFi.WebShareCz.ApiClient
{
    public class WsFilesPreviewReader : IEnumerable<WsFilePreview>, IDisposable
    {
        private readonly WsFilesPreviewReaderEngine _readerEngine;

        internal WsFilesPreviewReader(WsFilesPreviewReaderEngine readerEngine)
        {
            _readerEngine = readerEngine;
        }

        public void Dispose()
        {
            _readerEngine.Dispose();
        }

        public IEnumerator<WsFilePreview> GetEnumerator()
        {
            return _readerEngine.GetFilesPreview().GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return ((IEnumerable<WsFilePreview>)this).GetEnumerator();
        }
    }
}
