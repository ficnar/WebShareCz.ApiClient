using MaFi.WebShareCz.ApiClient.Entities;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace MaFi.WebShareCz.ApiClient
{
    internal sealed class WsPagedItemsReaderEngine : IWsItemsReaderEngine, IDisposable
    {
        public const int PAGE_SIZE = 30;
        private readonly WsApiClient _apiClient;
        private readonly WsFolderPath _folderPath;
        private readonly bool _useCreatedFileResolver;
        private readonly Func<int, Task<WsItemsReaderEngine>> _getReaderEngineMethod;
        private WsItemsReaderEngine _currentPageEngine;
        private Task<WsItemsReaderEngine> _nextPageEngineTask;
        private WsPagedItemsReaderEngine _childEngine;
        private bool _getItemsInvoked = false;
        private bool _disposed = false;

        public static async Task<WsPagedItemsReaderEngine> Create(WsApiClient apiClient, WsFolderPath folderPath, bool useCreatedFileResolver, Func<int, Task<WsItemsReaderEngine>> getReaderEngineMethod)
        {
            WsPagedItemsReaderEngine engine = new WsPagedItemsReaderEngine(apiClient, folderPath, useCreatedFileResolver, getReaderEngineMethod);
            engine._currentPageEngine = await getReaderEngineMethod(0);
            engine._nextPageEngineTask = getReaderEngineMethod(1);
            return engine;
        }

        private WsPagedItemsReaderEngine(WsApiClient apiClient, WsFolderPath folderPath, bool useCreatedFileResolver, Func<int, Task<WsItemsReaderEngine>> getReaderEngineMethod)
        {
            _apiClient = apiClient;
            _folderPath = folderPath;
            _useCreatedFileResolver = useCreatedFileResolver;
            _getReaderEngineMethod = getReaderEngineMethod;
        }

        public IEnumerable<WsItem> GetItems()
        {
            if (_disposed)
                throw new InvalidOperationException($"Enumerate in {nameof(WsPagedItemsReaderEngine)} is disposed");
            if (_getItemsInvoked)
                throw new InvalidOperationException($"Enumerate in {nameof(WsPagedItemsReaderEngine)} can call only ones");
            _getItemsInvoked = true;
            int currentPage = 0;
            CreatedFileResolver.IFolderItemsResolver folderItemsResolver = _useCreatedFileResolver ? _apiClient._createdFileResolver.FindFolderItemsResolver(_folderPath) : null;

            while (_currentPageEngine != null)
            {
                int readedItems = 0;
                foreach (WsItem item in _currentPageEngine.GetItems())
                {
                    readedItems++;
                    yield return item;
                }
                _currentPageEngine.Dispose();
                if (readedItems < PAGE_SIZE)
                    break;
                currentPage++;
                _currentPageEngine = _nextPageEngineTask.Result;
                _nextPageEngineTask = _getReaderEngineMethod(currentPage+1);
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

        public IEnumerable<WsFile> GetAllFilesRecursive(int depth)
        {
            return GetAllFilesRecursive(this, depth, 0);
        }

        public void Dispose()
        {
            _disposed = true;
            _getItemsInvoked = true;
            try
            {
                _nextPageEngineTask?.Result.Dispose();
            }
            catch { }
            _nextPageEngineTask = null;
            _currentPageEngine?.Dispose();
            _currentPageEngine = null;
            _childEngine?.Dispose();
            _childEngine = null;
        }

        private IEnumerable<WsFile> GetAllFilesRecursive(WsPagedItemsReaderEngine engine, int depth, int currentDepth)
        {
            List<WsFolderPath> folderPaths = new List<WsFolderPath>();
            foreach (WsItem item in engine.GetItems())
            {
                if (item is WsFile file)
                    yield return file;
                else if (item is WsFolder folder)
                    folderPaths.Add(folder.PathInfo);
            }
            currentDepth++;
            if (currentDepth <= depth)
            {
                foreach (WsFolderPath folderPath in folderPaths)
                {
                    using (WsPagedItemsReaderEngine childEngine = _apiClient.GetItems(folderPath).Result)
                    {
                        _childEngine = childEngine;
                        foreach (WsFile file in GetAllFilesRecursive(childEngine, depth, currentDepth))
                        {
                            yield return file;
                        }
                        _childEngine = null;
                    }
                }
            }
        }
    }
}
