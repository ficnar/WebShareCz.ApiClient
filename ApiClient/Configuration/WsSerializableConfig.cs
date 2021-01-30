using System;
using System.Linq;
using System.Runtime.Serialization;

namespace MaFi.WebShareCz.ApiClient.Configuration
{
    [DataContract]
    internal sealed class WsSerializableConfig
    {
        public WsSerializableConfig(WsConfig config)
        {
            DeviceUuid = config.DeviceUuid;
            Accounts = config.Accounts.Select(a => a.AccountConfig).ToArray();
        }

        [DataMember]
        public Guid DeviceUuid { get; private set; }

        [DataMember]
        public WsSerializableAccount[] Accounts { get; private set; }
    }
}
