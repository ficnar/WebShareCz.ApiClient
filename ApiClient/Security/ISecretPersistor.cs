namespace MaFi.WebShareCz.ApiClient.Security
{
    public interface ISecretPersistor
    {
        void SaveUserPasswordHash(string userPasswordHash);
    }
}
