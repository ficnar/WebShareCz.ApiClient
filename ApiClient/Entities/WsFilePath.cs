using System;

namespace MaFi.WebShareCz.ApiClient.Entities
{
    public sealed class WsFilePath : WsItemPath
    {
        public WsFilePath(string filePath, bool isPrivate) : base(CheckFilePath(filePath), isPrivate)
        {
        }

        public WsFilePath(WsFolderPath folder, string fileName) : base(folder.FullPath + fileName, folder.IsPrivate)
        {
        }


        public override WsFolderPath Folder => Parent;

        protected override WsFolderPath GetParent()
        {
            ParsePath(this.FullPath, out string folderPath, out _);
            return new WsFolderPath(folderPath, IsPrivate);
        }

        internal static string CheckFilePath(string filePath)
        {
            if (filePath?.EndsWith("/") == true)
                throw new ArgumentException("File path can not be ending with slash.", nameof(filePath));
            return filePath;
        }
    }
}
