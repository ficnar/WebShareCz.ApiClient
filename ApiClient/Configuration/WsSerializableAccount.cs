using System;
using System.Runtime.Serialization;

namespace MaFi.WebShareCz.ApiClient.Configuration
{
    [DataContract]
    internal sealed class WsSerializableAccount
    {
        public WsSerializableAccount(string userName)
        {
            if (string.IsNullOrWhiteSpace(userName))
                throw new ArgumentNullException(nameof(userName));
            UserName = userName;
        }

        [DataMember]
        public string UserName { get; private set; }

        [DataMember]
        public string UserPasswordHashEnc { get; set; }

    }
}
