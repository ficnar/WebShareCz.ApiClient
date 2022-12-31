using MaFi.WebShareCz.ApiClient.Entities;
using System;
using System.Collections;
using System.Collections.Generic;

namespace MaFi.WebShareCz.ApiClient
{
    public sealed class WsFilesReader : WsItemsReader
    {
        private readonly WsPagedItemsReaderEngine _readerEngine;
        private readonly int _depth;

        internal WsFilesReader(WsPagedItemsReaderEngine readerEngine, int depth) : base(readerEngine)
        {
            _readerEngine = readerEngine;
            _depth = depth;
        }

        public override IEnumerator<WsItem> GetEnumerator()
        {
            return _readerEngine.GetAllFilesRecursive(_depth).GetEnumerator();
        }
    }
}
