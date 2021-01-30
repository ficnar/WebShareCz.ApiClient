using MaFi.WebShareCz.ApiClient.Entities;

namespace MaFi.WebShareCz.ApiClient.Configuration
{
    public interface IConfigurationPersistor
    {
        WsConfig Load();

        void Save(WsConfig config);
    }
}
