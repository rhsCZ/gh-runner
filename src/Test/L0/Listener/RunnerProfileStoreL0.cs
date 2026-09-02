using System;
using System.IO;
using System.Linq;
using GitHub.Runner.Common;
using GitHub.Runner.Listener.MultiRepo;
using GitHub.Runner.Sdk;
using Xunit;

namespace GitHub.Runner.Common.Tests.Listener
{
    public sealed class RunnerProfileStoreL0
    {
        [Fact]
        [Trait("Level", "L0")]
        [Trait("Category", "Runner")]
        public void LoadProfiles_ReturnsConfiguredProfiles()
        {
            using var hc = new TestHostContext(this);
            var root = hc.GetDirectory(WellKnownDirectory.Root);
            var profilesRoot = Path.Combine(root, ".runner.d");
            var testPrefix = $"l0-{Guid.NewGuid():N}";

            try
            {
                CreateProfile(profilesRoot, $"{testPrefix}-repo-a", "runner-a", "token-a");
                CreateProfile(profilesRoot, $"{testPrefix}-repo-b", "runner-b", "token-b");

                var discovered = RunnerProfileStore.LoadProfiles(hc);

                Assert.Contains(discovered, x => x.Name == $"{testPrefix}-repo-a" && x.Settings.AgentName == "runner-a");
                Assert.Contains(discovered, x => x.Name == $"{testPrefix}-repo-b" && x.Settings.AgentName == "runner-b");
            }
            finally
            {
                foreach (var directory in Directory.EnumerateDirectories(profilesRoot, $"{testPrefix}*", SearchOption.TopDirectoryOnly))
                {
                    Directory.Delete(directory, recursive: true);
                }
            }
        }

        [Fact]
        [Trait("Level", "L0")]
        [Trait("Category", "Runner")]
        public void ActivateProfile_CopiesProfileConfigToRoot()
        {
            using var hc = new TestHostContext(this);
            var root = hc.GetDirectory(WellKnownDirectory.Root);
            var profilesRoot = Path.Combine(root, ".runner.d");
            var testPrefix = $"l0-{Guid.NewGuid():N}";
            string profileRoot = null;

            try
            {
                profileRoot = CreateProfile(profilesRoot, $"{testPrefix}-repo-c", "runner-c", "token-c");
                var profile = RunnerProfileStore.LoadProfiles(hc).Single(x => x.Name == $"{testPrefix}-repo-c");
                WriteHiddenRootConfig(root, ".runner", "{\"AgentName\":\"existing\"}");
                WriteHiddenRootConfig(root, ".credentials", "existing-credentials");
                WriteHiddenRootConfig(root, ".credentials_rsaparams", "existing-rsa");

                RunnerProfileStore.ActivateProfile(hc, profile);

                var runnerSettings = StringUtil.ConvertFromJson<RunnerSettings>(File.ReadAllText(Path.Combine(root, ".runner")));
                var credentials = IOUtil.LoadObject<CredentialData>(Path.Combine(root, ".credentials"));

                Assert.Equal("runner-c", runnerSettings.AgentName);
                Assert.Equal("token-c", credentials.Data[Constants.Runner.CommandLine.Args.Token]);
                Assert.True(File.Exists(Path.Combine(root, ".credentials_rsaparams")));
            }
            finally
            {
                foreach (var file in new[] { ".runner", ".credentials", ".runner_migrated", ".credentials_migrated", ".credentials_rsaparams" })
                {
                    var path = Path.Combine(root, file);
                    if (File.Exists(path))
                    {
                        File.Delete(path);
                    }
                }

                if (Directory.Exists(profileRoot))
                {
                    Directory.Delete(profileRoot, recursive: true);
                }
            }
        }

        [Fact]
        [Trait("Level", "L0")]
        [Trait("Category", "Runner")]
        public void CaptureAndRestoreActiveConfiguration_RestoresRootFiles()
        {
            using var hc = new TestHostContext(this);
            var root = hc.GetDirectory(WellKnownDirectory.Root);
            File.WriteAllText(Path.Combine(root, ".runner"), "{\"AgentName\":\"original\"}");

            var snapshot = RunnerProfileStore.CaptureActiveConfiguration(hc);
            File.WriteAllText(Path.Combine(root, ".runner"), "{\"AgentName\":\"mutated\"}");

            RunnerProfileStore.RestoreActiveConfiguration(hc, snapshot);

            var restored = File.ReadAllText(Path.Combine(root, ".runner"));
            Assert.Contains("original", restored);
        }

        private static string CreateProfile(string profilesRoot, string profileName, string runnerName, string token)
        {
            var profileRoot = Path.Combine(profilesRoot, profileName);
            Directory.CreateDirectory(profileRoot);

            var settings = new RunnerSettings
            {
                AgentId = 1,
                AgentName = runnerName,
                PoolId = 2,
                PoolName = "default",
                ServerUrl = "https://github.com/example/" + profileName,
                ServerUrlV2 = "https://broker.actions.githubusercontent.com/" + profileName,
                UseV2Flow = true,
                WorkFolder = "_work",
            };

            var credential = new CredentialData
            {
                Scheme = Constants.Configuration.OAuthAccessToken,
            };
            credential.Data[Constants.Runner.CommandLine.Args.Token] = token;

            File.WriteAllText(Path.Combine(profileRoot, ".runner"), StringUtil.ConvertToJson(settings));
            IOUtil.SaveObject(credential, Path.Combine(profileRoot, ".credentials"));
            File.WriteAllText(Path.Combine(profileRoot, ".credentials_rsaparams"), "rsa-placeholder");

            return profileRoot;
        }

        private static void WriteHiddenRootConfig(string root, string fileName, string content)
        {
            var path = Path.Combine(root, fileName);
            if (File.Exists(path))
            {
                File.SetAttributes(path, FileAttributes.Normal);
                File.Delete(path);
            }

            File.WriteAllText(path, content);
            File.SetAttributes(path, File.GetAttributes(path) | FileAttributes.Hidden);
        }
    }
}
