using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using GitHub.Runner.Common;
using GitHub.Runner.Common.Util;
using GitHub.Runner.Sdk;
using GitHub.Services.Common;
using GitHub.Services.OAuth;
using GitHub.Services.WebApi;

namespace GitHub.Runner.Listener.Configuration
{
    internal static class ProfileCredentialFactory
    {
        public static VssCredentials Create(IHostContext hostContext, CredentialData credentialData, bool allowAuthUrlV2, string profileRootPath = null)
        {
            ArgUtil.NotNull(hostContext, nameof(hostContext));
            ArgUtil.NotNull(credentialData, nameof(credentialData));

            if (string.Equals(credentialData.Scheme, Constants.Configuration.OAuth, StringComparison.OrdinalIgnoreCase) &&
                !string.IsNullOrEmpty(profileRootPath))
            {
                return CreateOAuthCredentials(hostContext, credentialData, allowAuthUrlV2, profileRootPath);
            }

            if (!CredentialManager.CredentialTypes.TryGetValue(credentialData.Scheme, out var credentialType))
            {
                throw new ArgumentException($"Unsupported credential scheme '{credentialData.Scheme}'.", nameof(credentialData));
            }

            var provider = Activator.CreateInstance(credentialType) as ICredentialProvider;
            ArgUtil.NotNull(provider, nameof(provider));
            provider.CredentialData = credentialData;
            return provider.GetVssCredentials(hostContext, allowAuthUrlV2);
        }

        private static VssCredentials CreateOAuthCredentials(IHostContext hostContext, CredentialData credentialData, bool allowAuthUrlV2, string profileRootPath)
        {
            var clientId = credentialData.Data.GetValueOrDefault("clientId", null);
            var authorizationUrl = credentialData.Data.GetValueOrDefault("authorizationUrl", null);
            var authorizationUrlV2 = credentialData.Data.GetValueOrDefault("authorizationUrlV2", null);

            if (allowAuthUrlV2 &&
                !string.IsNullOrEmpty(authorizationUrlV2) &&
                hostContext.AllowAuthMigration)
            {
                authorizationUrl = authorizationUrlV2;
            }

            var oauthEndpointUrl = credentialData.Data.GetValueOrDefault("oauthEndpointUrl", authorizationUrl);

            ArgUtil.NotNullOrEmpty(clientId, nameof(clientId));
            ArgUtil.NotNullOrEmpty(authorizationUrl, nameof(authorizationUrl));

            using var rsa = LoadProfileKey(profileRootPath);
            var keyParameters = rsa.ExportParameters(true);
            var signingCredentials = VssSigningCredentials.Create(
                () =>
                {
                    var signingKey = RSA.Create();
                    signingKey.ImportParameters(keyParameters);
                    return signingKey;
                },
                StringUtil.ConvertToBoolean(credentialData.Data.GetValueOrDefault("requireFipsCryptography"), false));
            var clientCredential = new VssOAuthJwtBearerClientCredential(clientId, authorizationUrl, signingCredentials);
            var agentCredential = new VssOAuthCredential(new Uri(oauthEndpointUrl, UriKind.Absolute), VssOAuthGrant.ClientCredentials, clientCredential);
            return new VssCredentials(agentCredential, CredentialPromptType.DoNotPrompt);
        }

        private static RSA LoadProfileKey(string profileRootPath)
        {
            var keyFile = Path.Combine(profileRootPath, ".credentials_rsaparams");
            if (!File.Exists(keyFile))
            {
                throw new CryptographicException($"RSA key file {keyFile} was not found");
            }

            var rsa = RSA.Create();
#if OS_WINDOWS
#pragma warning disable CA1416
            var encryptedBytes = File.ReadAllBytes(keyFile);
            var parametersString = Encoding.UTF8.GetString(ProtectedData.Unprotect(encryptedBytes, null, DataProtectionScope.LocalMachine));
            rsa.ImportParameters(StringUtil.ConvertFromJson<RSAParametersSerializable>(parametersString).RSAParameters);
#pragma warning restore CA1416
#else
            rsa.ImportParameters(IOUtil.LoadObject<RSAParametersSerializable>(keyFile).RSAParameters);
#endif
            return rsa;
        }
    }
}
