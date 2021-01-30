using System.Runtime.Serialization;

namespace MaFi.WebShareCz.ApiClient.Entities.Internals
{
    [DataContract(Name = ROOT_ELEMENT_NAME, Namespace = "")]
    internal sealed class LoginResult : Result
    {
        [DataMember(Name = "token", Order = 2)]
        public string Token { get; private set; }
    }
}
