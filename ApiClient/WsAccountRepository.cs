using System;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using MaFi.WebShareCz.ApiClient.Configuration;
using MaFi.WebShareCz.ApiClient.Entities;
using MaFi.WebShareCz.ApiClient.Security;

namespace MaFi.WebShareCz.ApiClient
{
    public sealed class WsAccountRepository : IEnumerable<WsAccount>
    {
        private readonly IConfigurationPersistor _persistor;
        private readonly List<WsAccount> _accounts;
        private readonly IDataProtector _protector;

        public WsAccountRepository(IDataProtector protector, string storeName = "default")
        {
            if (protector == null)
                throw new ArgumentNullException(nameof(protector));
            _protector = protector;
            _persistor = new FileConfigurationPersistor(storeName, this.Save, protector);
            WsAccount[] accounts;
            try
            {
                accounts = _persistor.Load().Accounts;
            }
            catch (Exception ex)
            {
                Trace.TraceError(ex.ToString());
                accounts = new WsAccount[0];
            }
            _accounts = new List<WsAccount>(accounts);
        }

        // TODO: caller need instance WsConfigurationSerializer depend on this method WsAccountRepository.Save (cyclic dependency)
        /*
        public WsAccountRepository(IConfigurationPersistor persistor)
        {
            _persistor = persistor;
            _accounts = new List<WsAccount>(persistor.Load());
        }*/

        public Guid GetDeviceUuid()
        {
            try
            {
                return _persistor.Load().DeviceUuid;
            }
            catch (Exception ex)
            {
                Trace.TraceError(ex.ToString());
                return Guid.NewGuid();
            }
        }

        public async Task<SuccessAccountRegistrationInfo> TryRegisterAccount(WsAccountLoginInfo userCredential)
        {
            if (userCredential == null)
                throw new ArgumentNullException(nameof(userCredential));
            if (_accounts.Exists(a => a.UserName.Equals(userCredential.UserName, StringComparison.InvariantCultureIgnoreCase)))
                throw new ArgumentException($"Account {userCredential.UserName} is already registered.", nameof(userCredential));

            WsApiClient apiClient = new WsApiClient(GetDeviceUuid());
            RegisterAccountSecretStore registerSecretStore = new RegisterAccountSecretStore(userCredential.UserPassword);
            bool successLogin = await apiClient.Login(userCredential.UserName, registerSecretStore, userCredential.RememberUserPassword ? registerSecretStore : null);
            if (successLogin)
            {
                WsAccount newAccount = new WsAccount(Save, _protector, userCredential.UserName, registerSecretStore.UserPasswordHash);
                _accounts.Add(newAccount);
                Save();
                return new SuccessAccountRegistrationInfo(newAccount, apiClient);
            }
            return null;
        }

        public bool UnRegisterAccount(WsAccount account)
        {
            if (_accounts.Remove(account))
            {
                Save();
                return true;
            }
            return false;
        }

        public IEnumerator<WsAccount> GetEnumerator()
        {
            return _accounts.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return this.GetEnumerator();
        }

        public int Count => _accounts.Count;


        public WsAccount this[int index]
        {
            get => _accounts[index];
        }


        public WsAccount this[string userName]
        {
            get => _accounts.First(a => a.UserName.Equals(userName, StringComparison.InvariantCultureIgnoreCase));
        }


        private void Save()
        {
            try
            {
                WsConfig config;
                try
                {
                    config = _persistor.Load();
                }
                catch (Exception ex)
                {
                    Trace.TraceError(ex.ToString());
                    config = new WsConfig();
                }
                config.Accounts = _accounts.ToArray();
                _persistor.Save(config);
            }
            catch (Exception ex)
            {
                Trace.TraceError(ex.ToString());
            }
        }

        public sealed class SuccessAccountRegistrationInfo
        {
            internal SuccessAccountRegistrationInfo(WsAccount account, WsApiClient connectedApiClient)
            {
                this.Account = account;
                this.ConnectedApiClient = connectedApiClient;
            }

            public WsAccount Account { get; }
            public WsApiClient ConnectedApiClient { get; }
        }

        private sealed class RegisterAccountSecretStore : ISecretProvider, ISecretPersistor
        {
            private readonly string _userPassword;

            public RegisterAccountSecretStore(string userPassword)
            {
                _userPassword = userPassword;
            }

            public string UserPasswordHash { get; private set; }

            public Task<string> GetPassword()
            {
                return Task.FromResult(_userPassword);
            }

            public bool TryGetUserPasswordHash(out string userPasswordHash)
            {
                userPasswordHash = null;
                return false;
            }

            public void SaveUserPasswordHash(string userPasswordHash)
            {
                UserPasswordHash = userPasswordHash;
            }
        }
    }
}
