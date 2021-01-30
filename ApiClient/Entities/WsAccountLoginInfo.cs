using System;
namespace MaFi.WebShareCz.ApiClient.Entities
{
    public sealed class WsAccountLoginInfo
    {
        public WsAccountLoginInfo(string userName, string userPassword, bool rememberUserPassword)
        {
            if (string.IsNullOrWhiteSpace(userName))
                throw new ArgumentNullException(nameof(userName));
            if (string.IsNullOrWhiteSpace(userName))
                throw new ArgumentNullException(nameof(userPassword));
            this.UserName = userName;
            this.UserPassword = userPassword;
            this.RememberUserPassword = rememberUserPassword;
        }

        public string UserName { get; }

        public string UserPassword { get; }

        public bool RememberUserPassword { get; }
    }
}
