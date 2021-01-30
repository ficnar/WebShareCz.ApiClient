using System;
using System.Diagnostics;
using System.Text;

namespace MaFi.WebShareCz.ApiClient.Security
{
    internal sealed class Protector
    {
        private readonly IDataProtector _protector;

        public Protector(IDataProtector protector)
        {
            _protector = protector;
        }

        public string Decrypt(string encryptedText)
        {
            try
            {
                byte[] encryptedData = Convert.FromBase64String(encryptedText);
                byte[] userData = _protector.Unprotect(encryptedData);
                return Encoding.UTF8.GetString(userData);
            }
            catch (Exception ex)
            {
                Trace.TraceError(ex.ToString());
                return null;
            }
        }

        public string Encrypt(string plainText)
        {
            try
            {
                byte[] userData = Encoding.UTF8.GetBytes(plainText);
                byte[] encryptedData = _protector.Protect(userData);
                return Convert.ToBase64String(encryptedData);
            }
            catch (Exception ex)
            {
                Trace.TraceError(ex.ToString());
                return null;
            }
        }
    }
}
