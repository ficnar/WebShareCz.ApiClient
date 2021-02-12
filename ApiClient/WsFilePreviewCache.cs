using System.Collections.Concurrent;
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

        private sealed class WsFolderCache
        {
            private readonly ConcurrentDictionary<string, WsFilePreview> _filesPreview = new ConcurrentDictionary<string, WsFilePreview>();
            private readonly Task _readerTask;
            private bool _clearRequest = false;

            public WsFolderCache(WsFolder folder)
            {
                _readerTask = ExecuteReader(folder);
            }

            private async Task ExecuteReader(WsFolder folder)
            {
                using (WsFilesPreviewReader reader = await folder.GetFilesPreview())
                {
                    foreach (WsFilePreview filePreview in reader)
                    {
                        if (_clearRequest)
                            break;
                        _filesPreview.AddOrUpdate(filePreview.Name, filePreview, (fileName, filePreviewExists) => filePreviewExists);
                    }
                }
            }

            public async Task<WsFilePreview> FindFilePreview(string fileName)
            {
                await _readerTask;
                if (_filesPreview.TryGetValue(fileName, out WsFilePreview filePreview))
                    return filePreview;
                return new WsFilePreview(fileName);
            }

            public void Clear()
            {
                _clearRequest = true;
                _readerTask.GetAwaiter().GetResult();
                foreach (WsFilePreview filePreview in _filesPreview.Values)
                {
                    filePreview.Dispose();
                }
                _filesPreview.Clear();
            }
        }
    }
}
