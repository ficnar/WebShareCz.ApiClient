﻿using System;
using System.Collections;
using System.Collections.Generic;
using MaFi.WebShareCz.ApiClient.Entities;

namespace MaFi.WebShareCz.ApiClient
{
    public class WsItemsReader : IEnumerable<WsItem>, IDisposable
    {
        private readonly WsPagedItemsReaderEngine _readerEngine;

        internal WsItemsReader(WsPagedItemsReaderEngine readerEngine)
        {
            _readerEngine = readerEngine;
        }

        public void Dispose()
        {
            _readerEngine.Dispose();
        }

        public virtual IEnumerator<WsItem> GetEnumerator()
        {
            return _readerEngine.GetItems().GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return ((IEnumerable <WsItem>)this).GetEnumerator();
        }
    }
}
