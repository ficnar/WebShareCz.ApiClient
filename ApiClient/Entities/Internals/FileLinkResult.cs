using System.Runtime.Serialization;

namespace MaFi.WebShareCz.ApiClient.Entities.Internals
{
    [DataContract(Name = ROOT_ELEMENT_NAME, Namespace = "")]
    internal sealed class FileLinkResult : Result
    {
        [DataMember(Name = "link", Order = 2)]
        public string Link { get; private set; }
    }
}
