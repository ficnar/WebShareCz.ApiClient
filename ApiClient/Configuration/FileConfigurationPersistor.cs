using MaFi.WebShareCz.ApiClient.Security;
using System;
using System.IO;

namespace MaFi.WebShareCz.ApiClient.Configuration
{
    internal sealed class FileConfigurationPersistor : IConfigurationPersistor
    {
        private readonly FileInfo _storeFile;
        private readonly WsConfigSerializer _serializer;

        public FileConfigurationPersistor(string storeName, Action onChange, IDataProtector protector)
        {
            if (string.IsNullOrWhiteSpace(storeName))
                throw new ArgumentNullException(nameof(storeName));
            if (onChange == null)
                throw new ArgumentNullException(nameof(onChange));
            _serializer = new WsConfigSerializer(onChange, protector);
            _storeFile = new FileInfo(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Mafi.WebShareCz.ApiClient", $"{storeName}.json"));
        }

        public WsConfig Load()
        {
            if (_storeFile.Exists == false)
            {
                WsConfig config = new WsConfig();
                Save(config);
                return config;
            }
            using (FileStream stream = _storeFile.OpenRead())
            {
                return _serializer.Deserialize(stream);
            }
        }

        public void Save(WsConfig config)
        {
            if (_storeFile.Directory.Exists == false)
            {
                _storeFile.Directory.Create();
                _storeFile.Directory.Refresh();
            }
            using (FileStream stream = _storeFile.Create())
            {
                _serializer.Serialize(stream, config);
            }
            _storeFile.Refresh();
        }
    }
}
