using System;
using System.Linq;
using MaFi.WebShareCz.ApiClient.Entities;
using MaFi.WebShareCz.ApiClient.Security;

namespace MaFi.WebShareCz.ApiClient.Configuration
{
    public sealed class WsConfig
    {
        internal WsConfig()
        {
            DeviceUuid = Guid.NewGuid();
            Accounts = new WsAccount[0];
        }

        internal WsConfig (WsSerializableConfig serializableConfig, Action onChange, IDataProtector protector)
        {
            DeviceUuid = serializableConfig.DeviceUuid;
            Accounts = serializableConfig.Accounts.Select(c => new WsAccount(c, onChange, protector)).ToArray();
        }

        public Guid DeviceUuid { get; }

        public WsAccount[] Accounts { get; internal set; }
    }
}
