using System;
using MaFi.WebShareCz.ApiClient.Configuration;
using MaFi.WebShareCz.ApiClient.Security;

namespace MaFi.WebShareCz.ApiClient.Entities
{
    public sealed class WsAccount
    {
        private readonly Action _onChange;
        private readonly Protector _protector;

        internal WsAccount(WsSerializableAccount accountConfig, Action onChange, IDataProtector protector)
        {
            AccountConfig = accountConfig;
            _onChange = onChange;
            _protector = new Protector(protector);
        }

        internal WsAccount(Action onChange, IDataProtector protector, string userName, string userPasswordHash = null) : this(new WsSerializableAccount(userName), onChange, protector)
        {
            if (string.IsNullOrWhiteSpace(userPasswordHash) == false)
                AccountConfig.UserPasswordHashEnc = _protector.Encrypt(userPasswordHash);
        }

        internal WsSerializableAccount AccountConfig { get; }

        public string UserName => AccountConfig.UserName;

        public bool TryGetUserPasswordHash(out string userPasswordHash)
        {
            if (string.IsNullOrWhiteSpace(AccountConfig.UserPasswordHashEnc))
                userPasswordHash = null;
            else
                userPasswordHash = _protector.Decrypt(AccountConfig.UserPasswordHashEnc);
            return userPasswordHash != null;
        }

        public void SaveUserPasswordHash(string userPasswordHash)
        {
            if (string.IsNullOrWhiteSpace(userPasswordHash))
                AccountConfig.UserPasswordHashEnc = null;
            else
                AccountConfig.UserPasswordHashEnc = _protector.Encrypt(userPasswordHash);
            _onChange();
        }

        public override string ToString()
        {
            return this.UserName;
        }

        public override int GetHashCode()
        {
            return this.UserName.ToLower().GetHashCode();
        }

        public override bool Equals(object obj)
        {
            if (obj is WsAccount)
                return ToString().Equals(obj.ToString(), StringComparison.InvariantCultureIgnoreCase);
            return false;
        }
    }
}
