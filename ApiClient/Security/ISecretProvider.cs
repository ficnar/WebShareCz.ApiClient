using System.Threading.Tasks;

namespace MaFi.WebShareCz.ApiClient.Security
{
    public interface ISecretProvider
    {
        Task<string> GetPassword();

        bool TryGetUserPasswordHash(out string userPasswordHash);
    }
}
