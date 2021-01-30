using System.Runtime.Serialization;

namespace MaFi.WebShareCz.ApiClient.Entities.Internals
{
    [DataContract(Name = ROOT_ELEMENT_NAME, Namespace = "")]
    internal sealed class CreateFolderResult : Result
    {
        [DataMember(Name = "ident", Order = 2)]
        public string Ident { get; private set; }

        [DataMember(Name = "name", Order = 3)]
        public string Name { get; private set; }

        [DataMember(Name = "path", Order = 4)]
        public string Path { get; private set; }
    }
}
