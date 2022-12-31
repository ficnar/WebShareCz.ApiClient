using System;
using System.Linq;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using MaFi.WebShareCz.ApiClient.Entities;
using System.Collections.Generic;

namespace MaFi.WebShareCz.ApiClient
{
    internal sealed class CreatedFileResolver
    {
        private readonly IEnumerable<WsFile> _empty = new WsFile[0];
        private readonly ConcurrentDictionary<WsFolderPath, FolderItemsResolver> _dic = new ConcurrentDictionary<WsFolderPath, FolderItemsResolver>();
        private readonly WsApiClient _apiClient;

        public CreatedFileResolver(WsApiClient apiClient)
        {
            _apiClient = apiClient;
        }

        public IEnumerable<WsFile> GetCreatedFilesInProgress(WsFolderPath folderPath)
        {
            if (_dic.TryGetValue(folderPath, out FolderItemsResolver folderItemsResolver))
                return folderItemsResolver.GetCreatedFilesInProgress();
            return _empty;
        }

        public WsFile Add(WsFolderPath folderPath, string fileName, string fileIdent, long fileSize)
        {
            FolderItemsResolver folderItemsResolver = _dic.GetOrAdd(
                folderPath,
                x => new FolderItemsResolver(folderPath, _apiClient, RemoveFolder));
            return folderItemsResolver.Add(fileName, fileIdent, fileSize, folderPath.IsPrivate);
        }

        public void RemoveItem(WsItem item)
        {
            if (item is WsFile file)
                FindFolderItemsResolver(file.PathInfo.Folder)?.RemoveFileAndFolderIfEmpty(file);
            else if (item is WsFolder folder)
            {
                string folderPath = folder.PathInfo.Folder.FullPath;
                KeyValuePair<WsFolderPath, FolderItemsResolver>[] removingFolders = _dic.Where(e => e.Key.IsPrivate == folder.PathInfo.IsPrivate && e.Key.FullPath.StartsWith(folderPath)).ToArray();
                foreach (KeyValuePair<WsFolderPath, FolderItemsResolver> removingFolder in removingFolders)
                {
                    RemoveFolder(removingFolder.Key);
                    _ = removingFolder.Value.Clear(); // no wait for ending
                }
            }
        }

        public void RenameItem(WsItem item, string newName)
        {
            if (item is WsFile file)
                (FindFolderItemsResolver(file.PathInfo.Folder) as FolderItemsResolver)?.Rename(file, newName);
            else if (item is WsFolder folder)
                MoveFolder(folder.PathInfo.Folder, new WsFolderPath(folder.PathInfo.Parent.FullPath + newName, folder.PathInfo.IsPrivate));
        }

        public void MoveItem(WsItem item, WsFolder targetFolder)
        {
            FolderItemsResolver sourceFolderItemsResolver = (FindFolderItemsResolver(item.PathInfoGeneric.Folder) as FolderItemsResolver);
            if (sourceFolderItemsResolver == null)
                return;
            if (item is WsFile file)
            {
                sourceFolderItemsResolver.RemoveFileAndFolderIfEmpty(file);
                if (file.IsReady == false)
                    Add(targetFolder.PathInfo, file.PathInfo.Name, file.Ident, file.Size);
            }
            else if (item is WsFolder folder)
                MoveFolder(folder.PathInfo, targetFolder.PathInfo);
        }

        public IFolderItemsResolver FindFolderItemsResolver(WsFolderPath folderPath)
        {
            _dic.TryGetValue(folderPath, out FolderItemsResolver folderItemsResolver);
            return folderItemsResolver;
        }

        public async Task Clear()
        {
            FolderItemsResolver[] folderItemsResolvers = _dic.Values.ToArray();
            _dic.Clear();
            await Task.WhenAll(folderItemsResolvers.Select(r => r.Clear()));
        }

        public interface IFolderItemsResolver
        {
            bool RemoveFileAndFolderIfEmpty(WsFile foundedFile);

            IEnumerable<WsFile> GetCreatedFilesInProgress();
        }

        private void MoveFolder(WsFolderPath baseFolder, WsFolderPath baseNewFolder)
        {
            KeyValuePair<WsFolderPath, FolderItemsResolver>[] renamingFolders = _dic.Where(e => e.Key.IsPrivate == baseFolder.IsPrivate && e.Key.FullPath.StartsWith(baseFolder.FullPath)).ToArray();
            foreach (KeyValuePair<WsFolderPath, FolderItemsResolver> renamingFolder in renamingFolders)
            {
                if (RemoveFolder(renamingFolder.Key, out FolderItemsResolver folderItemsResolver))
                {
                    WsFolderPath newFolderPath = new WsFolderPath(baseNewFolder.FullPath + renamingFolder.Key.FullPath.Substring(baseNewFolder.FullPath.Length), baseNewFolder.IsPrivate);
                    folderItemsResolver.ChangeFolderPath(newFolderPath);
                    if (_dic.TryAdd(newFolderPath, folderItemsResolver))
                        Log($"Rename/move folder {newFolderPath}", renamingFolder.Key);
                }
            }
        }

        private void RemoveFolder(WsFolderPath folderPath)
        {
            RemoveFolder(folderPath, out _);
        }

        private bool RemoveFolder(WsFolderPath folderPath, out FolderItemsResolver folderItemsResolver)
        {
            if (_dic.TryRemove(folderPath, out folderItemsResolver))
            {
                Log("Remove folder", folderPath);
                return true;
            }
            return false;
        }

        //private static readonly object _syncRoot = new object();
        private static void Log(string action, WsItemPath path)
        {
            System.Diagnostics.Trace.TraceInformation($"CreatedFileResolver - {action}: {path}");
            //lock (_syncRoot)
            //{
            //    System.IO.File.AppendAllText(@"c:\temp\trace.txt", $"{DateTime.Now.ToLongTimeString()} - {action}: {path}\r\n");
            //}
        }

        private sealed class FolderItemsResolver : IFolderItemsResolver
        {
            private readonly ConcurrentDictionary<string, WsFile> _dic = new ConcurrentDictionary<string, WsFile>();
            private readonly WsApiClient _apiClient;
            private readonly Action<WsFolderPath> _removeFromParent;
            private WsFolderPath _folderPath;
            private CancellationTokenSource _cancellationTokenSource;
            private Task _resolverTask;

            public FolderItemsResolver(WsFolderPath folderPath, WsApiClient apiClient, Action<WsFolderPath> removeFromParent)
            {
                _folderPath = folderPath;
                _apiClient = apiClient;
                _removeFromParent = removeFromParent;
                Log("Add folder", folderPath);
            }

            public IEnumerable<WsFile> GetCreatedFilesInProgress()
            {
                return _dic.Values;
            }

            public WsFile Add(string fileName, string fileIdent, long fileSize, bool isPrivate)
            {
                return _dic.GetOrAdd(fileIdent, x =>
                {
                    if (_resolverTask == null || _resolverTask.IsCompleted || _resolverTask.IsCanceled || _resolverTask.IsFaulted)
                    {
                        _cancellationTokenSource = new CancellationTokenSource();
                        _resolverTask = ContinuosResolve(_cancellationTokenSource.Token);
                    }
                    WsFile newFile = new WsFile(_folderPath, fileName, fileIdent, fileSize, _apiClient);
                    Log("Add file", newFile.PathInfo);
                    return newFile;
                });
            }

            public async Task Clear()
            {
                _removeFromParent(_folderPath);
                _cancellationTokenSource?.Cancel();
                if (_resolverTask != null)
                {
                    await _resolverTask;
                    CleanContinuosResolve();
                }
                _dic.Clear();
                Log("Cancel folder", _folderPath);
            }

            public bool RemoveFileAndFolderIfEmpty(WsFile foundedFile)
            {
                if (_dic.TryRemove(foundedFile.Ident, out WsFile removedFile))
                {
                    Log("Remove file", foundedFile.PathInfo);
                    foundedFile.ChangeStateToReady();
                    removedFile.ChangeStateToReady();
                    if (_dic.Count == 0)
                    {
                        _removeFromParent(_folderPath);
                        return true;
                    }
                }
                return false;
            }

            public void Rename(WsFile file, string newName)
            {
                if (_dic.TryGetValue(file.Ident, out WsFile newFile))
                {
                    newFile.ChangeNameInInstance(newName);
                    Log($"Rename file to {newName}", file.PathInfo);
                }
            }

            public void ChangeFolderPath(WsFolderPath newFolderPath)
            {
                _folderPath = newFolderPath;
                foreach (WsFile file in _dic.Values)
                {
                    file.ChangeFolderPathInInstance(newFolderPath.FullPath, newFolderPath.IsPrivate);
                }
            }

            private void CleanContinuosResolve()
            {
                _cancellationTokenSource?.Dispose();
                _resolverTask = null;
                _cancellationTokenSource = null;
            }

            private async Task ContinuosResolve(CancellationToken cancellationToken)
            {
                int delay = 1000;
                bool firstCall = true;
                while (cancellationToken.IsCancellationRequested == false && (_dic.Count > 0 || firstCall))
                {
                    firstCall = false;
                    try
                    {
                        await Task.Delay(delay, cancellationToken);
                        WsFile[] maxFiveWaitingFilesOrNull = _dic.Count > 5 ? new WsFile[] { null } : _dic.Values.ToArray();
                        string[] allWaitingFilesIdents = _dic.Values.Select(f => f.Ident).ToArray();
                        foreach (WsFile waitingFileOrNull in maxFiveWaitingFilesOrNull)
                        {
                            if (cancellationToken.IsCancellationRequested)
                                return;
                            using (IWsItemsReaderEngine reader = await _apiClient.GetFolderItemsWithoutCreatedFilesInProgress(_folderPath, waitingFileOrNull?.PathInfo.Name ?? ""))
                            {
                                if (cancellationToken.IsCancellationRequested)
                                    return;
                                foreach (WsFile foundedFile in reader.GetItems().OfType<WsFile>().Where(f => f.Ident == waitingFileOrNull?.Ident || allWaitingFilesIdents.Contains(f.Ident)))
                                {
                                    if (cancellationToken.IsCancellationRequested)
                                        return;
                                    if (RemoveFileAndFolderIfEmpty(foundedFile))
                                    {
                                        CleanContinuosResolve();
                                        return;
                                    }
                                }
                            }
                        }
                    }
                    catch (Exception)
                    { }
                    if (delay < (1000 * 2 * 2))
                        delay *= 2;
                }
            }
        }
    }
}
