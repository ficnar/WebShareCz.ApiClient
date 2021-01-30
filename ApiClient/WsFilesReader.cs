using MaFi.WebShareCz.ApiClient.Entities;
using System;
using System.Collections;
using System.Collections.Generic;

namespace MaFi.WebShareCz.ApiClient
{
    public class WsFilesReader : WsItemsReader
    {
        private readonly WsItemsReaderEngine _readerEngine;
        private readonly int _depth;

        internal WsFilesReader(WsItemsReaderEngine readerEngine, int depth) : base(readerEngine)
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
