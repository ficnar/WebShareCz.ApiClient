using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Runtime.Serialization;
using System.Threading.Tasks;
using System.Xml;
using MaFi.WebShareCz.ApiClient.Entities;
using MaFi.WebShareCz.ApiClient.Entities.Internals;


namespace MaFi.WebShareCz.ApiClient
{
    internal sealed class WsItemsReaderEngine : Result, IWsItemsReaderEngine, IDisposable
    {
        private readonly WsApiClient _apiClient;
        private readonly WsFolderPath _folderPath;
        private readonly bool _useCreatedFileResolver;
        private HttpResponseMessage _responseMessage;
        private XmlNodeReader _xmlReader;
        private bool _getItemsInvoked = false;
        private bool _disposed = false;

        public static async Task<WsItemsReaderEngine> Create(WsApiClient apiClient, HttpResponseMessage responseMessage, WsFolderPath folderPath, bool useCreatedFileResolver)
        {
            WsItemsReaderEngine engine = new WsItemsReaderEngine(apiClient, responseMessage, folderPath, useCreatedFileResolver);
            XmlDocument xml = new XmlDocument();
            xml.Load(await engine._responseMessage.Content.ReadAsStreamAsync());
            engine._xmlReader = new XmlNodeReader(xml.DocumentElement);

            if (engine._xmlReader.Read() && engine._xmlReader.Name == ROOT_ELEMENT_NAME && engine._xmlReader.Read() && engine._xmlReader.Name == "status")
            {
                engine.Status = engine._xmlReader.ReadElementContentAsString();
                if (engine.Status != ResultStatus.OK)
                {
                    if (engine._xmlReader.Name == "code")
                        engine.ErrorCode = engine._xmlReader.ReadElementContentAsString();
                    engine.Dispose();
                }
            }
            else
            {
                engine.Status = "Xml format error.";
                engine.Dispose();
            }
            return engine;
        }

        private WsItemsReaderEngine(WsApiClient apiClient, HttpResponseMessage responseMessage, WsFolderPath folderPath, bool useCreatedFileResolver)
        {
            _apiClient = apiClient;
            _responseMessage = responseMessage;
            _folderPath = folderPath;
            _useCreatedFileResolver = useCreatedFileResolver;
        }

        public IEnumerable<WsItem> GetItems()
        {
            if (_disposed)
                throw new InvalidOperationException($"Enumerate in {nameof(WsItemsReaderEngine)} is disposed");
            if (_getItemsInvoked)
                throw new InvalidOperationException($"Enumerate in {nameof(WsItemsReaderEngine)} can call only ones");
            _getItemsInvoked = true;
            CreatedFileResolver.IFolderItemsResolver folderItemsResolver = _useCreatedFileResolver ? _apiClient._createdFileResolver.FindFolderItemsResolver(_folderPath) : null;
            while (AppVersion == 0 && _disposed == false)
            {
                if (_xmlReader.Name == "folder")
                    yield return CreateItemInfo<WsFolder>();
                else if (_xmlReader.Name == "file")
                {
                    WsFile file = CreateItemInfo<WsFile>();
                    if (file.IsReady)
                    {
                        if (folderItemsResolver?.RemoveFileAndFolderIfEmpty(file) == true)
                            folderItemsResolver = null;
                        yield return file;
                    }
                }
                else if (_xmlReader.Name == "app_version")
                    AppVersion = _xmlReader.ReadElementContentAsInt();
                else
                    break;
            }
            Dispose();
            if (folderItemsResolver != null)
            {
                foreach (WsFile file in folderItemsResolver.GetCreatedFilesInProgress())
                {
                    yield return file;
                }
            }
        }

        public void Dispose()
        {
            _disposed = true;
            _getItemsInvoked = true;
            _xmlReader?.Dispose();
            _xmlReader = null;
            _responseMessage?.Dispose();
            _responseMessage = null;
        }

        private TItemInfo CreateItemInfo<TItemInfo>() where TItemInfo : WsItem
        {
            XmlReader subReader = _xmlReader.ReadSubtree();
            DataContractSerializer serializer = new DataContractSerializer(typeof(TItemInfo));
            TItemInfo item = (TItemInfo)serializer.ReadObject(subReader);
            item.Init(_apiClient, _folderPath.IsPrivate);
            _xmlReader.Read();
            return item;
        }
    }
}
