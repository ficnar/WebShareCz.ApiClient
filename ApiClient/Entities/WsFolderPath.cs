using System;

namespace MaFi.WebShareCz.ApiClient.Entities
{
    public sealed class WsFolderPath : WsItemPath
    {
        public WsFolderPath(string folderPath, bool isPrivate) : base(CanonizeFolderPath(folderPath), isPrivate)
        {
        }

        public override WsFolderPath Folder => this;

        protected override WsFolderPath GetParent()
        {
            ParseFolderPath(FullPath, out string parentFolderPath, out _);
            if (parentFolderPath == null)
                return null;
            return new WsFolderPath(parentFolderPath, IsPrivate);
        }

        private static string CanonizeFolderPath(string folderPath)
        {
            return CombinePath(folderPath, "");
        }

        private static string CombinePath(string folderPath, string fileName)
        {
            if (folderPath == null)
                throw new ArgumentNullException(nameof(folderPath));
            if (fileName == null)
                throw new ArgumentNullException(nameof(fileName));
            if (folderPath.EndsWith("/") == false)
                folderPath += "/";
            return folderPath + fileName;
        }

        private static void ParseFolderPath(string folderPath, out string parentFolderPath, out string folderName)
        {
            if (folderPath == "/")
            {
                parentFolderPath = null;
                folderName = "";
            }
            else
            {
                if (folderPath.EndsWith("/"))
                    folderPath = folderPath.Substring(0, folderPath.Length - 1);
                ParsePath(folderPath, out parentFolderPath, out folderName);
            }
        }
    }
}
