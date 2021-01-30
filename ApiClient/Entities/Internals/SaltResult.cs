using System.Runtime.Serialization;

namespace MaFi.WebShareCz.ApiClient.Entities.Internals
{
    [DataContract(Name = ROOT_ELEMENT_NAME, Namespace = "")]
    internal sealed class SaltResult : Result
    {
        [DataMember(Name = "salt", Order = 2)]
        public string Salt { get; private set; }
    }
}
