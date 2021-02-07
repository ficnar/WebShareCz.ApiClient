using System;
using System.IO;
using System.Runtime.Serialization;
using System.Threading;
using System.Threading.Tasks;

namespace MaFi.WebShareCz.ApiClient.Entities
{
    [DataContract(Namespace = "")]
    public abstract class WsItem
    {
        protected const string READY_STATE = "ready";
        protected const string DELETED_STATE = "deleted";

        internal WsItem()
        {
        }

        internal void Init(WsApiClient apiClient, bool isPrivate)
        {
            ApiClient = apiClient;
            InitPathInfo(isPrivate);
        }

        protected abstract void InitPathInfo(bool isPrivate);

        protected WsApiClient ApiClient { get; private set; }

        public async Task Delete()
        {
            if (PathInfoGeneric.FullPath == "/")
                throw new IOException("Can not delete root folder");
            await ApiClient.DeleteItem(this);
            State = DELETED_STATE;
        }

        public Task Rename(string newName)
        {
            if (PathInfoGeneric.FullPath == "/")
                throw new IOException("Can not rename root folder");
            return ApiClient.RenameItem(this, newName);
        }

        public Task Move(WsFolder targetFolder)
        {
            if (PathInfoGeneric.FullPath == "/")
                throw new IOException("Can not move root folder");
            return ApiClient.MoveItem(this, targetFolder);
        }

        public Task<WsItem> Copy(WsFolder targetFolder, CancellationToken cancellationToken, IProgress<int> progress = null)
        {
            if (PathInfoGeneric.FullPath == "/")
                throw new IOException("Can not copy root folder");
            return ApiClient.CopyItem(this, targetFolder, cancellationToken, progress);
        }

        internal abstract void ChangeNameInInstance(string newName);

        internal abstract void ChangePathInInstance(string newPath, bool newIsPrivate);

        public abstract WsItemPath PathInfoGeneric { get; }

        public bool IsReady => State == READY_STATE;

        [DataMember(Name = "ident", Order = 1)]
        public string Ident { get; protected set; }

        public DateTime Created { get; protected set; }

        protected string State { get; set; }

        public override string ToString()
        {
            return ApiClient != null && PathInfoGeneric != null ? $"{ApiClient.UserName}{PathInfoGeneric}" : "[Not initialized]";
        }

        public override int GetHashCode()
        {
            return ToString().ToLower().GetHashCode();
        }

        public override bool Equals(object obj)
        {
            if (obj is WsItem)
                return ToString().Equals(obj.ToString(), StringComparison.InvariantCultureIgnoreCase);
            return false;
        }
    }
}
