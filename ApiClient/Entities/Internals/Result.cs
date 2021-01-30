using System.Runtime.Serialization;

namespace MaFi.WebShareCz.ApiClient.Entities.Internals
{
    [DataContract(Name = ROOT_ELEMENT_NAME, Namespace = "")]
    internal class Result
    {
        public const string ROOT_ELEMENT_NAME = "response";

        [DataMember(Name = "status", Order = 1)]
        public string Status { get; protected set; }

        [DataMember(Name = "code", Order = 2)]
        public string ErrorCode { get; protected set; }

        [DataMember(Name = "app_version", Order = 999)]
        public int AppVersion { get; protected set; }
    }
}
