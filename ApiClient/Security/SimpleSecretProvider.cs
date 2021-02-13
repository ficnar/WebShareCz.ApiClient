using System.Threading.Tasks;

namespace MaFi.WebShareCz.ApiClient.Security
{
    internal sealed class SimpleSecretProvider : ISecretProvider
    {
        private readonly string _userPassword;

        public SimpleSecretProvider(string userPassword)
        {
            _userPassword = userPassword;
        }
        public Task<string> GetPassword()
        {
            return Task.FromResult(_userPassword);
        }

        public bool TryGetUserPasswordHash(out string userPasswordHash)
        {
            userPasswordHash = null;
            return false;
        }
    }
}
