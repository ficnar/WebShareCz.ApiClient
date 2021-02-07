using System;
using System.Linq;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading.Tasks;
using MaFi.WebShareCz.ApiClient.Entities;

namespace MaFi.WebShareCz.ApiClient
{
    public sealed class WsFilePreviewCache
    {
        private readonly ConcurrentDictionary<WsFolder, WsFolderCache> _folders = new ConcurrentDictionary<WsFolder, WsFolderCache>();

        public Task<WsFilePreview> FindFilePreview(WsFolder folder, string fileName)
        {
            WsFolderCache folderCache = _folders.GetOrAdd(folder, (folder) => new WsFolderCache(folder));
            return folderCache.FindFilePreview(fileName);
        }

        public void Clear()
        {
            foreach (WsFolderCache folderCache in _folders.Values)
            {
                folderCache.Clear();
            }
            _folders.Clear();
        }

        private sealed class WsFolderCache : IDisposable
        {
            private readonly ConcurrentDictionary<string, WsFilePreview> _filesPreview = new ConcurrentDictionary<string, WsFilePreview>();
            private readonly Task<WsFilesPreviewReader> _readerTask;
            private Task<IEnumerator<WsFilePreview>> _enumeratorTask;

            public WsFolderCache(WsFolder folder)
            {
                _readerTask = folder.GetFilesPreview();
                _enumeratorTask = ExecuteEnumerator();
            }

            private async Task<IEnumerator<WsFilePreview>> ExecuteEnumerator()
            {
                WsFilesPreviewReader reader = await _readerTask;
                IEnumerator<WsFilePreview> enumerator = reader.GetEnumerator();
                if (enumerator.MoveNext() == false)
                {
                    enumerator = null;
                    reader.Dispose();
                }
                return enumerator;
            }

            public async Task<WsFilePreview> FindFilePreview(string fileName)
            {
                Dictionary<string, WsFilePreview> newFilesPreview = new Dictionary<string, WsFilePreview>();
                IEnumerator<WsFilePreview> enumerator = _enumeratorTask == null ? null : await _enumeratorTask;
                WsFilePreview filePreview = _filesPreview.GetOrAdd(fileName, (fileName) =>
                {
                    if (enumerator != null)
                    {
                        WsFilePreview newFilePreview = null;
                        do
                        {
                            if (enumerator.Current.Name == fileName)
                                newFilePreview = enumerator.Current;
                            else
                                newFilesPreview.Add(enumerator.Current.Name, enumerator.Current);
                            if (enumerator.MoveNext() == false)
                                Dispose();
                            if (newFilePreview != null)
                                return newFilePreview;
                        }
                        while (_enumeratorTask != null);
                    }
                    return new WsFilePreview(fileName);
                });
                foreach (var item in newFilesPreview)
                {
                    _filesPreview.AddOrUpdate(item.Key, item.Value, (fileName, filePreviewExists) => filePreviewExists);
                }
                return filePreview;
            }

            public void Clear()
            {
                Dispose();
                foreach (WsFilePreview filePreview in _filesPreview.Values)
                {
                    filePreview.Dispose();
                }
                _filesPreview.Clear();
            }

            public void Dispose()
            {
                _enumeratorTask = null;
                _readerTask.GetAwaiter().GetResult().Dispose();
            }
        }
    }
}
