using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using GitHub.Runner.Common;
using GitHub.Runner.Sdk;

namespace GitHub.Runner.Listener.MultiRepo
{
    internal static class RunnerProfileStore
    {
        private const string ProfilesDirectoryName = ".runner.d";
        private static readonly WellKnownConfigFile[] s_profileFiles =
        [
            WellKnownConfigFile.Runner,
            WellKnownConfigFile.Credentials,
            WellKnownConfigFile.MigratedRunner,
            WellKnownConfigFile.MigratedCredentials,
            WellKnownConfigFile.RSACredentials,
        ];

        public static IReadOnlyList<RunnerProfile> LoadProfiles(IHostContext hostContext)
        {
            ArgUtil.NotNull(hostContext, nameof(hostContext));

            var profilesRoot = GetProfilesRoot(hostContext);
            if (!Directory.Exists(profilesRoot))
            {
                return Array.Empty<RunnerProfile>();
            }

            var profiles = new List<RunnerProfile>();
            foreach (var directory in Directory.EnumerateDirectories(profilesRoot).OrderBy(x => x, StringComparer.OrdinalIgnoreCase))
            {
                var runnerFile = Path.Combine(directory, GetFileName(WellKnownConfigFile.Runner));
                var credentialFile = Path.Combine(directory, GetFileName(WellKnownConfigFile.Credentials));
                if (!File.Exists(runnerFile) || !File.Exists(credentialFile))
                {
                    continue;
                }

                var settingsJson = File.ReadAllText(runnerFile, Encoding.UTF8);
                var settings = StringUtil.ConvertFromJson<RunnerSettings>(settingsJson);
                var credential = IOUtil.LoadObject<CredentialData>(credentialFile);

                profiles.Add(new RunnerProfile
                {
                    Name = Path.GetFileName(directory),
                    RootPath = directory,
                    Settings = settings,
                    Credential = credential,
                });
            }

            return profiles;
        }

        public static ActiveConfigurationSnapshot CaptureActiveConfiguration(IHostContext hostContext)
        {
            ArgUtil.NotNull(hostContext, nameof(hostContext));

            var snapshot = new ActiveConfigurationSnapshot();
            var root = hostContext.GetDirectory(WellKnownDirectory.Root);
            foreach (var configFile in s_profileFiles)
            {
                var fileName = GetFileName(configFile);
                var path = Path.Combine(root, fileName);
                if (File.Exists(path))
                {
                    snapshot.Files[fileName] = File.ReadAllBytes(path);
                }
            }

            return snapshot;
        }

        public static void RestoreActiveConfiguration(IHostContext hostContext, ActiveConfigurationSnapshot snapshot)
        {
            ArgUtil.NotNull(hostContext, nameof(hostContext));
            ArgUtil.NotNull(snapshot, nameof(snapshot));

            var root = hostContext.GetDirectory(WellKnownDirectory.Root);
            foreach (var configFile in s_profileFiles)
            {
                var fileName = GetFileName(configFile);
                var target = Path.Combine(root, fileName);
                if (snapshot.Files.TryGetValue(fileName, out var bytes))
                {
                    ReplaceFile(target, bytes);
                }
                else if (File.Exists(target))
                {
                    IOUtil.DeleteFile(target);
                }
            }
        }

        public static void SaveActiveConfigurationAsProfile(IHostContext hostContext, string profileName)
        {
            ArgUtil.NotNull(hostContext, nameof(hostContext));
            ArgUtil.NotNullOrEmpty(profileName, nameof(profileName));

            var root = hostContext.GetDirectory(WellKnownDirectory.Root);
            var profileRoot = GetProfileRoot(hostContext, profileName);
            Directory.CreateDirectory(profileRoot);

            foreach (var configFile in s_profileFiles)
            {
                var fileName = GetFileName(configFile);
                var source = Path.Combine(root, fileName);
                var target = Path.Combine(profileRoot, fileName);
                if (File.Exists(source))
                {
                    ReplaceFile(source, target);
                }
                else if (File.Exists(target))
                {
                    IOUtil.DeleteFile(target);
                }
            }
        }

        public static void ActivateProfile(IHostContext hostContext, RunnerProfile profile)
        {
            ArgUtil.NotNull(hostContext, nameof(hostContext));
            ArgUtil.NotNull(profile, nameof(profile));

            var root = hostContext.GetDirectory(WellKnownDirectory.Root);
            foreach (var configFile in s_profileFiles)
            {
                var source = Path.Combine(profile.RootPath, GetFileName(configFile));
                var target = Path.Combine(root, GetFileName(configFile));

                if (File.Exists(source))
                {
                    ReplaceFile(source, target);
                }
                else if (File.Exists(target))
                {
                    IOUtil.DeleteFile(target);
                }
            }
        }

        public static bool ProfileExists(IHostContext hostContext, string profileName)
        {
            ArgUtil.NotNull(hostContext, nameof(hostContext));
            ArgUtil.NotNullOrEmpty(profileName, nameof(profileName));
            return Directory.Exists(GetProfileRoot(hostContext, profileName));
        }

        public static void DeleteProfile(IHostContext hostContext, string profileName)
        {
            ArgUtil.NotNull(hostContext, nameof(hostContext));
            ArgUtil.NotNullOrEmpty(profileName, nameof(profileName));

            var profileRoot = GetProfileRoot(hostContext, profileName);
            if (Directory.Exists(profileRoot))
            {
                Directory.Delete(profileRoot, recursive: true);
            }
        }

        private static string GetProfilesRoot(IHostContext hostContext)
        {
            return Path.Combine(hostContext.GetDirectory(WellKnownDirectory.Root), ProfilesDirectoryName);
        }

        private static string GetProfileRoot(IHostContext hostContext, string profileName)
        {
            return Path.Combine(GetProfilesRoot(hostContext), profileName);
        }

        private static string GetFileName(WellKnownConfigFile configFile)
        {
            return configFile switch
            {
                WellKnownConfigFile.Runner => ".runner",
                WellKnownConfigFile.Credentials => ".credentials",
                WellKnownConfigFile.MigratedRunner => ".runner_migrated",
                WellKnownConfigFile.MigratedCredentials => ".credentials_migrated",
                WellKnownConfigFile.RSACredentials => ".credentials_rsaparams",
                _ => throw new NotSupportedException($"Unsupported profile config file '{configFile}'."),
            };
        }

        private static void ReplaceFile(string source, string target)
        {
            if (File.Exists(target))
            {
                File.SetAttributes(target, FileAttributes.Normal);
                File.Delete(target);
            }

            File.Copy(source, target, overwrite: false);
            File.SetAttributes(target, File.GetAttributes(target) | FileAttributes.Hidden);
        }

        private static void ReplaceFile(string target, byte[] contents)
        {
            if (File.Exists(target))
            {
                File.SetAttributes(target, FileAttributes.Normal);
                File.Delete(target);
            }

            File.WriteAllBytes(target, contents);
            File.SetAttributes(target, File.GetAttributes(target) | FileAttributes.Hidden);
        }
    }
}
