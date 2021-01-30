using System.Runtime.Serialization;

namespace MaFi.WebShareCz.ApiClient.Entities.Internals
{
    [DataContract(Name = ROOT_ELEMENT_NAME, Namespace = "")]
    internal sealed class UploadUrlResult : Result
    {
        [DataMember(Name = "url", Order = 2)]
        public string Url { get; private set; }

        [DataMember(Name = "code", Order = 2)]
        public string Code { get; private set; }
    }
}

