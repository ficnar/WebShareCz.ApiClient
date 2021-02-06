using MaFi.WebShareCz.ApiClient.Entities.Internals;
using System;
using System.Globalization;
using System.IO;
using System.Runtime.Serialization;
using System.Threading;
using System.Threading.Tasks;

namespace MaFi.WebShareCz.ApiClient.Entities
{
    [DataContract(Name = "folder", Namespace = "")]
    public sealed class WsFolder : WsItem
    {
        internal static WsFolder CreateRootFolder(WsApiClient apiClient, bool isPrivate)
        {
            WsFolder folder = new WsFolder();
            folder.PathInternal = "/";
            folder.State = "ready";
            folder.Init(apiClient, isPrivate);
            return folder;
        }

        /// <summary>
        /// Internal for deserialization from json
        /// </summary>
        internal WsFolder()
        {
        }

        internal WsFolder(CreateFolderResult newFolder, bool isPrivate, WsApiClient apiClient)
        {
            Ident = newFolder.Ident;
            PathInternal = newFolder.Path;
            Created = DateTime.Now;
            State = "ready";
            Init(apiClient, isPrivate);
        }

        protected override void InitPathInfo(bool isPrivate)
        {
            PathInfo = new WsFolderPath(PathInternal, isPrivate);
            PathInternal = null;
        }

        public WsFolderPath PathInfo { get; private set; }

        public override WsItemPath PathInfoGeneric => PathInfo;

        public Task<WsItemsReader> GetChildItems()
        {
            return ApiClient.GetFolderItems(PathInfo);
        }

        public Task<WsFilesReader> GetAllFilesRecursive(int depth)
        {
            return ApiClient.GetFolderAllFilesRecursive(PathInfo, depth);
        }
        
        public Task<WsFilesPreviewReader> GetFilesPreview()
        {
            return ApiClient.GetFolderFilesPreview(this);
        }

        public Task<WsFile> UploadFile(FileStream sourceStream, string fileName, CancellationToken cancellationToken, IProgress<int> progress = null)
        {
            return UploadFile(sourceStream, sourceStream.Length, fileName, cancellationToken, progress);
        }

        public Task<WsFile> UploadFile(Stream sourceStream, long sourceSize, string fileName, CancellationToken cancellationToken, IProgress<int> progress = null)
        {
            return ApiClient.UploadFile(sourceStream, sourceSize, new WsFilePath(PathInfo, fileName), cancellationToken, progress);
        }

        public Task<WsFolder> CreateSubFolder(string name)
        {
            return ApiClient.CreateFolder(PathInfo.FullPath + name, PathInfo.IsPrivate);
        }

        public override string ToString()
        {
            return $"{nameof(WsFolder)}: {base.ToString()}";
        }

        internal override void ChangeNameInInstance(string newName)
        {
            PathInfo = new WsFolderPath(PathInfo.Parent.FullPath + newName, PathInfo.IsPrivate);
        }

        internal override void ChangePathInInstance(string newPath, bool newIsPrivate)
        {
            PathInfo = new WsFolderPath(newPath, newIsPrivate);
        }

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
