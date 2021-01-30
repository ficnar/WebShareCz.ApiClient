using System;
using System.Globalization;
using System.IO;
using System.Runtime.Serialization;
using System.Threading;
using System.Threading.Tasks;

namespace MaFi.WebShareCz.ApiClient.Entities
{
    [DataContract(Name = "file", Namespace = "")]
    public sealed class WsFile : WsItem
    {
        private TaskCompletionSource<object> _tcs = null;

        /// <summary>
        /// Internal for deserialization from json
        /// </summary>
        internal WsFile()
        {
        }

        internal WsFile(WsItemPath folderPath, string fileName, string ident, long size, WsApiClient apiClient)
        {
            Ident = ident;
            PathInternal = folderPath.FullPath + fileName;
            Size = size == -1 ? 0 : size;
            Created = DateTime.Now;
            State = "";
            Init(apiClient, folderPath.IsPrivate);
        }

        protected override void InitPathInfo(bool isPrivate)
        {
            PathInfo = new WsFilePath(PathInternal, isPrivate);
            PathInternal = null;
        }

        public WsFilePath PathInfo { get; private set; }

        public override WsItemPath PathInfoGeneric => PathInfo;

        public Task Download(Stream targetStream, CancellationToken cancellationToken, IProgress<int> progress = null)
        {
            return ApiClient.DownloadFile(this, targetStream, cancellationToken, progress);
        }

        public Task Replace(FileStream sourceStream, CancellationToken cancellationToken, IProgress<int> progress = null)
        {
            return Replace(sourceStream, sourceStream.Length, cancellationToken, progress);
        }

        public async Task Replace(Stream sourceStream, long sourceSize, CancellationToken cancellationToken, IProgress<int> progress = null)
        {
            await Delete();
            await ApiClient.UploadFile(sourceStream, sourceSize, PathInfo, false, cancellationToken, progress);
        }

        public override string ToString()
        {
            return $"{nameof(WsFile)}: {base.ToString()}";
        }

        internal async Task WaitForReadyFile(CancellationToken cancellationToken = new CancellationToken())
        {
            if (State == DELETED_STATE)
                throw new IOException($"File {PathInfo.FullPath} was deleted.");
            if (IsReady == false)
            {
                if (_tcs == null)
                    _tcs = new TaskCompletionSource<object>();
                try
                {
                    using (cancellationToken.Register(() => _tcs.TrySetCanceled()))
                    {
                        // TODO: tady to asi zamrzne, pokud souběžně běží jiná operace, napříkald kopírování souborů
                        await _tcs.Task;
                    }
                }
                finally
                {
                    _tcs = null;
                }
            }
        }

        internal void ChangeStateToReady()
        {
            if (State == DELETED_STATE)
                throw new InvalidOperationException($"File {PathInfo.FullPath} was deleted.");
            if (State != READY_STATE)
            {
                State = READY_STATE;
                _tcs?.TrySetResult(null);
            }
        }

        internal void ChangeFolderPathInInstance(string newFolderPath, bool newIsPrivate)
        {
            PathInfo = new WsFilePath(newFolderPath + PathInfo.Name, newIsPrivate);
        }

        internal override void ChangeNameInInstance(string newName)
        {
            PathInfo = new WsFilePath(PathInfo.Parent.FullPath + newName, PathInfo.IsPrivate);
        }

        internal override void ChangePathInInstance(string newPath, bool newIsPrivate)
        {
            PathInfo = new WsFilePath(newPath, newIsPrivate);
        }

        [DataMember(Name = "size", Order = 3)]
        public long Size { get; private set; }

        [DataMember(Name = "created", Order = 6)]
        private string CreatedInternal
        {
            get => Created.ToString(CultureInfo.GetCultureInfo("cs"));
            set => Created = DateTime.Parse(value, CultureInfo.GetCultureInfo("cs"));
        }

        [DataMember(Name = "state", Order = 7)]
        private string StateInternal { get => State; set => State = value; }

        [DataMember(Name = "path", Order = 11)]
        private string PathInternal { get; set; }
    }
}
