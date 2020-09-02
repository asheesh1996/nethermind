using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Nethermind.Logging;
using Nethermind.Vault.Config;
using provide.Model.Vault;

namespace Nethermind.Vault
{
    public class VaultService : IVaultService
    {
        private static readonly Dictionary<string, object> EmptyQuery = new Dictionary<string, object>();
        
        private readonly ILogger _logger;
        
        private readonly IVaultConfig _vaultConfig;

        private string _host;
        
        private string _path;
        
        private string _scheme;
        
        private string _token;

        private provide.Vault _vaultService;

        public VaultService(IVaultConfig vaultConfig, ILogManager logManager)
        {
            _vaultConfig = vaultConfig ?? throw new ArgumentNullException(nameof(vaultConfig));
            _logger = logManager.GetClassLogger() ?? throw new ArgumentNullException(nameof(logManager));

            _host = _vaultConfig.Host;
            _path = _vaultConfig.Path;
            _scheme = _vaultConfig.Scheme;
            _token = _vaultConfig.Token;
            _vaultService = new provide.Vault(_host, _path, _scheme, _token);
        }

        public Task ResetToken(string token)
        {
            _token = token;
            InitVaultService();
            return Task.CompletedTask;
        }

        public Task Reset(string scheme, string host, string path, string token)
        {
            _scheme = scheme;
            _host = host;
            _path = path;
            _token = token;
            InitVaultService();
            return Task.CompletedTask;
        }

        public async Task<IEnumerable<Guid>> ListVaultIds()
        {
            List<provide.Model.Vault.Vault> result = await _vaultService.ListVaults(EmptyQuery);
            return result
                .Where(v => v.Id != null)
                .Select(v => v.Id.Value);
        }

        public async Task<provide.Model.Vault.Vault> CreateVault(provide.Model.Vault.Vault vault)
        {
            if(_logger.IsDebug) _logger.Debug($"Creating a vault {vault.Name} {vault.Description}");
            provide.Model.Vault.Vault result = await _vaultService.CreateVault(vault);
            return result;
        }

        public async Task<provide.Model.Vault.Vault> DeleteVault(Guid vaultId)
        {
            if(_logger.IsDebug) _logger.Debug($"Deleting vault {vaultId}");
            return await _vaultService.DeleteVault(vaultId.ToString());
        }

        public async Task<IEnumerable<Key>> ListKeys(Guid vaultId)
        {
            if(_logger.IsDebug) _logger.Debug("Listing keys");
            return await _vaultService.ListVaultKeys(vaultId.ToString(), EmptyQuery);
        }

        public async Task<Key> CreateKey(Guid vaultId, Key key)
        {
            if(_logger.IsDebug) _logger.Debug($"Creating a key named {key.Name} in the vault {vaultId}");
            Key vaultKey = await _vaultService.CreateVaultKey(vaultId.ToString(), key);
            return vaultKey;
        }
        
        public async Task<Key> DeleteKey(Guid vaultId, Guid keyId)
        {
            if(_logger.IsDebug) _logger.Debug($"Deleting the key {keyId} in the vault {vaultId}");
            Key vaultKey = await _vaultService.DeleteVaultKey(vaultId.ToString(), keyId.ToString());
            return vaultKey;
        }

        public async Task<IEnumerable<Secret>> ListSecrets(Guid vaultId)
        {
            if(_logger.IsDebug) _logger.Debug("Listing secrets");
            return await _vaultService.ListVaultSecrets(vaultId.ToString(), EmptyQuery);
        }

        public async Task<Secret> CreateSecret(Guid vaultId, Secret secret)
        {
            if(_logger.IsDebug) _logger.Debug($"Creating a secret in the vault {vaultId}");
            return await _vaultService.CreateVaultSecret(
                vaultId.ToString(), secret);
        }

        public async Task<Secret> DeleteSecret(Guid vaultId, Guid secretId)
        {
            if(_logger.IsDebug) _logger.Debug($"Deleting the secret {secretId} in the vault {vaultId}");
            return await _vaultService.DeleteVaultSecret(
                vaultId.ToString(), secretId.ToString());
        }

        public async Task<string> Sign(Guid vaultId, Guid keyId, string message)
        {
            if(_logger.IsDebug) _logger.Debug($"Signing a message with the key {keyId} from the vault {vaultId}");
            SignedMessage result = await _vaultService.SignMessage(
                vaultId.ToString(), keyId.ToString(), message);
            return result.Signature;
        }
        
        public async Task<bool> Verify(Guid vaultId, Guid keyId, string message, string signature)
        {
            if(_logger.IsDebug) _logger.Debug($"Verifying a message with the key {keyId} from the vault {vaultId}");
            SignedMessage result = await _vaultService.VerifySignature(
                vaultId.ToString(), keyId.ToString(), message, signature);
            return result.Verified;
        }

        private void InitVaultService()
        {
            if(_logger.IsDebug) _logger.Debug($"Initializing a vault service for {_host} {_path} {_scheme}");
            _vaultService = new provide.Vault(_host, _path, _scheme, _token);
        }
    }
}