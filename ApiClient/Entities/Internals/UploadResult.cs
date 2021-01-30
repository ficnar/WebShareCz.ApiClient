using System;
using System.Collections.Generic;
using System.Runtime.Serialization;
using System.Text;

namespace MaFi.WebShareCz.ApiClient.Entities.Internals
{
    [DataContract()]
    internal sealed class UploadResult : Result
    {
        [DataMember(Name = "jsonrpc")]
        public string JsonRpc { get; private set; }

        [DataMember(Name = "result")]
        public string Result { get; private set; }

        [DataMember(Name = "id")]
        public string Id { get; private set; }

        [DataMember(Name = "ident")]
        public string Ident { get; private set; }
    }
}
