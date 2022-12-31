using MaFi.WebShareCz.ApiClient.Entities;
using System;
using System.Collections.Generic;

namespace MaFi.WebShareCz.ApiClient
{
    internal interface IWsItemsReaderEngine : IDisposable
    {
        IEnumerable<WsItem> GetItems();
    }
}
