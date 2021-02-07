using MaFi.WebShareCz.ApiClient.Entities;
using MaFi.WebShareCz.ApiClient.Entities.Internals;
using System;
using System.Linq;
using System.Collections.Generic;
using System.Net.Http;
using System.Runtime.Serialization;
using System.Threading.Tasks;
using System.Xml;
using MaFi.WebShareCz.ApiClient.Http;

namespace MaFi.WebShareCz.ApiClient
{
    internal sealed class WsFilesPreviewReaderEngine : Result, IDisposable
    {
        private readonly WsHttpClient _httpClient;
        private HttpResponseMessage _responseMessage;
        private XmlNodeReader _xmlReader;
        private bool _getItemsInvoked = false;
        private bool _disposed = false;
        private List<string> _fileIdents = null;

        public static async Task<WsFilesPreviewReaderEngine> Create(WsHttpClient httpClient, Task<WsFilesReader> filesReaderTask, HttpResponseMessage responseMessage)
        {
            WsFilesPreviewReaderEngine engine = new WsFilesPreviewReaderEngine(httpClient, responseMessage);
            XmlDocument xml = new XmlDocument();
            xml.Load(await engine._responseMessage.Content.ReadAsStreamAsync());
            engine._xmlReader = new XmlNodeReader(xml.DocumentElement);

            if (engine._xmlReader.Read() && engine._xmlReader.Name == ROOT_ELEMENT_NAME && engine._xmlReader.Read() && engine.ReadNextElement(out string status) == "status")
            {
                engine.Status = status;
                if (engine.Status != ResultStatus.OK)
                {
                    if (engine.ReadNextElement(out string errorCode) == "code")
                        engine.ErrorCode = errorCode;
                    engine.Dispose();
                }
                else if (engine.ReadNextElement(out _) != "name" || engine.ReadNextElement(out _) != "total" || engine.ReadNextElement(out _) != "size")
                {
                    engine.Status = "Xml format error.";
                    engine.Dispose();
                }
            }
            else
            {
                engine.Status = "XML format error.";
                engine.Dispose();
            }

            using (WsFilesReader filesReader = await filesReaderTask)
            {
                if (engine._disposed == false)
                    engine._fileIdents = filesReader.Select(f => f.Ident).ToList();
            }
            return engine;
        }

        private WsFilesPreviewReaderEngine(WsHttpClient httpClient, HttpResponseMessage responseMessage)
        {
            _httpClient = httpClient;
            _responseMessage = responseMessage;
        }

        public IEnumerable<WsFilePreview> GetFilesPreview()
        {
            if (_disposed)
                throw new InvalidOperationException($"Enumerate in {nameof(WsItemsReaderEngine)} is disposed");
            if (_getItemsInvoked)
                throw new InvalidOperationException($"Enumerate in {nameof(WsItemsReaderEngine)} can call only ones");
            _getItemsInvoked = true;
            while (AppVersion == 0 && _disposed == false && _fileIdents.Count > 0)
            {
                if (_xmlReader.Name == "file")
                {
                    WsFilePreview filePreview = CreateFilePreview();
                    if (_fileIdents.Contains(filePreview.Ident))
                    {
                        filePreview.StartDownload(_httpClient);
                        _fileIdents.Remove(filePreview.Ident);
                        yield return filePreview;
                    }
                }
                else if (_xmlReader.Name == "app_version")
                    AppVersion = _xmlReader.ReadElementContentAsInt();
                else
                    break;
            }
            Dispose();
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

        private string ReadNextElement(out string value)
        {
            string name = _xmlReader.Name;
            value = _xmlReader.ReadElementContentAsString();
            return name;
        }

        private WsFilePreview CreateFilePreview()
        {
            XmlReader subReader = _xmlReader.ReadSubtree();
            DataContractSerializer serializer = new DataContractSerializer(typeof(WsFilePreview));
            WsFilePreview item = (WsFilePreview)serializer.ReadObject(subReader);
            _xmlReader.Read();
            return item;
        }

    }
}
