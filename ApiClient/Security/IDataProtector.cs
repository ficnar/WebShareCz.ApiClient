namespace MaFi.WebShareCz.ApiClient.Security
{
    public interface IDataProtector
    {
        byte[] Protect(byte[] userData);

        byte[] Unprotect(byte[] encryptedData);
    }
}
