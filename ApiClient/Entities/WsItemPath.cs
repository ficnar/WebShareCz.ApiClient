using System;

namespace MaFi.WebShareCz.ApiClient.Entities
{
    public abstract class WsItemPath
    {
        protected WsItemPath(string fullPath, bool isPrivate)
        {
            if (string.IsNullOrEmpty(fullPath))
                throw new ArgumentNullException(nameof(fullPath));
            if (fullPath.StartsWith("/") == false)
                throw new NotSupportedException($"Relative paths are not supported! path: '{fullPath}'");
            FullPath = fullPath;
            IsPrivate = isPrivate;
            Parent = GetParent();
        }

        public string FullPath { get; }

        public abstract WsFolderPath Folder { get; }

        public WsFolderPath Parent { get; }

        public string Name
        {
            get
            {
                string path = FullPath.EndsWith("/") ? FullPath.Substring(0, FullPath.Length - 1) : FullPath;
                int lastSlash = path.LastIndexOf("/");
                return lastSlash == -1 ? string.Empty : path.Substring(lastSlash + 1);
            }
        }

        public bool IsPrivate { get; }

        public override string ToString()
        {
            return $"/{(IsPrivate ? "private" : "public")}{FullPath}";
        }

        public override int GetHashCode()
        {
            return this.ToString().ToLower().GetHashCode();
        }

        public override bool Equals(object obj)
        {
            if (obj is WsItemPath)
                return ToString().Equals(obj.ToString(), StringComparison.InvariantCultureIgnoreCase);
            return false;
        }

        protected abstract WsFolderPath GetParent();

        protected static void ParsePath(string path, out string folderPath, out string fileName)
        {
            int lastSlash = path.LastIndexOf("/");
            folderPath = path.Substring(0, lastSlash + 1);
            fileName = folderPath == path ? "" : path.Substring(lastSlash + 1);
        }
    }
}
