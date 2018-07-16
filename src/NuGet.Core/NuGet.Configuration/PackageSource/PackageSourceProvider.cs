// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;

#if !IS_CORECLR
using NuGet.Common;
#endif

namespace NuGet.Configuration
{
    public class PackageSourceProvider : IPackageSourceProvider
    {
        private ITrustedSourceProvider _trustedSourceProvider;

        public ISettings Settings { get; private set; }

        private const int MaxSupportedProtocolVersion = 3;
        private readonly IDictionary<PackageSource, PackageSource> _migratePackageSources;
        private readonly IEnumerable<PackageSource> _configurationDefaultSources;

        public PackageSourceProvider(ISettings settings)
            : this(settings, migratePackageSources: null)
        {
        }

        public PackageSourceProvider(
          ISettings settings,
          IDictionary<PackageSource, PackageSource> migratePackageSources)
            : this(settings,
                  migratePackageSources,
                  ConfigurationDefaults.Instance.DefaultPackageSources)
        {
        }

        public PackageSourceProvider(
            ISettings settings,
            IDictionary<PackageSource, PackageSource> migratePackageSources,
            IEnumerable<PackageSource> configurationDefaultSources
            )
        {
            Settings = settings ?? throw new ArgumentNullException(nameof(settings));
            Settings.SettingsChanged += (_, __) => { OnPackageSourcesChanged(); };
            _migratePackageSources = migratePackageSources;
            _configurationDefaultSources = LoadConfigurationDefaultSources(configurationDefaultSources);
            _trustedSourceProvider = new TrustedSourceProvider(Settings);
        }

        private IEnumerable<PackageSource> LoadConfigurationDefaultSources(IEnumerable<PackageSource> configurationDefaultSources)
        {
#if !IS_CORECLR
            // Global default NuGet source doesn't make sense on Mono
            if (RuntimeEnvironmentHelper.IsMono)
            {
                return Enumerable.Empty<PackageSource>();
            }
#endif
            var packageSourceLookup = new Dictionary<string, IndexedPackageSource>(StringComparer.OrdinalIgnoreCase);
            var packageIndex = 0;

            foreach (var packageSource in configurationDefaultSources)
            {
                packageIndex = AddOrUpdateIndexedSource(packageSourceLookup, packageIndex, packageSource);
            }

            return packageSourceLookup.Values
                .OrderBy(source => source.Index)
                .Select(source => source.PackageSource);
        }

        /// <summary>
        /// Returns PackageSources if specified in the config file. Else returns the default sources specified in the
        /// constructor.
        /// If no default values were specified, returns an empty sequence.
        /// </summary>
        public IEnumerable<PackageSource> LoadPackageSources()
        {
            var packageSourcesSection = Settings.Sections[ConfigurationConstants.PackageSources];
            var sources = packageSourcesSection?.Children.Select(s => s as SourceElement)
                .Where(s => s != null);

            // get list of disabled packages
            var disabledSourcesSection = Settings.Sections[ConfigurationConstants.DisabledPackageSources];
            var disabledSourcesSettings = disabledSourcesSection?.Children.Select(s => s as AddElement)
                .Where(s => s != null);

            var disabledSources = new Dictionary<string, AddElement>(StringComparer.OrdinalIgnoreCase);
            foreach (var setting in disabledSourcesSettings)
            {
                if (disabledSources.ContainsKey(setting.Key))
                {
                    disabledSources[setting.Key] = setting;
                }
                else
                {
                    disabledSources.Add(setting.Key, setting);
                }
            }

            var packageSourceLookup = new Dictionary<string, IndexedPackageSource>(StringComparer.OrdinalIgnoreCase);
            var packageIndex = 0;
            foreach (var setting in sources)
            {
                var name = setting.Key;

                var isEnabled = true;
                if (disabledSources.TryGetValue(name, out var disabledSource))
                {
                    isEnabled = false;
                }

                var packageSource = ReadPackageSource(setting, isEnabled);
                packageIndex = AddOrUpdateIndexedSource(packageSourceLookup, packageIndex, packageSource);
            }

            var loadedPackageSources = packageSourceLookup.Values
                .OrderBy(source => source.Index)
                .Select(source => source.PackageSource)
                .ToList();

            if (_configurationDefaultSources != null && _configurationDefaultSources.Any())
            {
                SetDefaultPackageSources(loadedPackageSources);
            }

            if (_migratePackageSources != null)
            {
                MigrateSources(loadedPackageSources);
            }

            return loadedPackageSources;
        }

        private void SetDefaultPackageSources(List<PackageSource> loadedPackageSources)
        {
            var defaultPackageSourcesToBeAdded = new List<PackageSource>();

            foreach (var packageSource in _configurationDefaultSources)
            {
                var sourceMatching = loadedPackageSources.Any(p => p.Source.Equals(packageSource.Source, StringComparison.CurrentCultureIgnoreCase));
                var feedNameMatching = loadedPackageSources.Any(p => p.Name.Equals(packageSource.Name, StringComparison.CurrentCultureIgnoreCase));

                if (!sourceMatching && !feedNameMatching)
                {
                    defaultPackageSourcesToBeAdded.Add(packageSource);
                }
            }

            var defaultSourcesInsertIndex = loadedPackageSources.FindIndex(source => source.IsMachineWide);

            if (defaultSourcesInsertIndex == -1)
            {
                defaultSourcesInsertIndex = loadedPackageSources.Count;
            }

            loadedPackageSources.InsertRange(defaultSourcesInsertIndex, defaultPackageSourcesToBeAdded);
        }

        private PackageSource ReadPackageSource(SourceElement setting, bool isEnabled)
        {
            var name = setting.Key;
            var packageSource = new PackageSource(setting.Value, name, isEnabled)
            {
                IsMachineWide = setting.Origin.IsMachineWide
            };

            var credentials = ReadCredential(name);
            if (credentials != null)
            {
                packageSource.Credentials = credentials;
            }

            var trustedSource = ReadTrustedSource(name);
            if (trustedSource != null)
            {
                packageSource.TrustedSource = trustedSource;
            }

            packageSource.ProtocolVersion = ReadProtocolVersion(setting);

            return packageSource;
        }

        private static int ReadProtocolVersion(SourceElement setting)
        {
            if (int.TryParse(setting.ProtocolVersion, out var protocolVersion))
            {
                return protocolVersion;
            }

            return PackageSource.DefaultProtocolVersion;
        }

        private static int AddOrUpdateIndexedSource(
            Dictionary<string, IndexedPackageSource> packageSourceLookup,
            int packageIndex,
            PackageSource packageSource)
        {
            if (!packageSourceLookup.TryGetValue(packageSource.Name, out var previouslyAddedSource))
            {
                packageSourceLookup[packageSource.Name] = new IndexedPackageSource
                {
                    PackageSource = packageSource,
                    Index = packageIndex++
                };
            }
            else if (previouslyAddedSource.PackageSource.ProtocolVersion < packageSource.ProtocolVersion
                     &&
                     packageSource.ProtocolVersion <= MaxSupportedProtocolVersion)
            {
                // Pick the package source with the highest supported protocol version
                previouslyAddedSource.PackageSource = packageSource;
            }

            return packageIndex;
        }

        private TrustedSource ReadTrustedSource(string name)
        {
            return _trustedSourceProvider.LoadTrustedSource(name);
        }

        private PackageSourceCredential ReadCredential(string sourceName)
        {
            var environmentCredentials = ReadCredentialFromEnvironment(sourceName);

            if (environmentCredentials != null)
            {
                return environmentCredentials;
            }

            var credentialsSection = Settings.Sections[ConfigurationConstants.CredentialsSectionName];
            var sourceSubsection = credentialsSection.Children.Select(c => c as SettingSection).Where(s => s != null).FirstOrDefault(s => string.Equals(s.Name, sourceName, StringComparison.Ordinal));
            if (sourceSubsection != null && sourceSubsection.Children != null && sourceSubsection.Children.Any())
            {
                var userName = (sourceSubsection.GetChildElement(ConfigurationConstants.KeyAttribute, ConfigurationConstants.UsernameToken) as AddElement)?.Value;

                if (!string.IsNullOrEmpty(userName))
                {
                    var encryptedPassword = (sourceSubsection.GetChildElement(ConfigurationConstants.KeyAttribute, ConfigurationConstants.PasswordToken) as AddElement)?.Value;
                    if (!string.IsNullOrEmpty(encryptedPassword))
                    {
                        return new PackageSourceCredential(sourceName, userName, encryptedPassword, isPasswordClearText: false);
                    }

                    var clearTextPassword = (sourceSubsection.GetChildElement(ConfigurationConstants.KeyAttribute, ConfigurationConstants.ClearTextPasswordToken) as AddElement)?.Value;
                    if (!string.IsNullOrEmpty(clearTextPassword))
                    {
                        return new PackageSourceCredential(sourceName, userName, clearTextPassword, isPasswordClearText: true);
                    }
                }
            }

            return null;
        }

        private PackageSourceCredential ReadCredentialFromEnvironment(string sourceName)
        {
            var rawCredentials = Environment.GetEnvironmentVariable("NuGetPackageSourceCredentials_" + sourceName);
            if (string.IsNullOrEmpty(rawCredentials))
            {
                return null;
            }

            var match = Regex.Match(rawCredentials.Trim(), @"^Username=(?<user>.*?);\s*Password=(?<pass>.*?)$", RegexOptions.IgnoreCase);
            if (!match.Success)
            {
                return null;
            }

            return new PackageSourceCredential(
                sourceName,
                match.Groups["user"].Value,
                match.Groups["pass"].Value,
                isPasswordClearText: true);
        }

        private void MigrateSources(List<PackageSource> loadedPackageSources)
        {
            var hasChanges = false;
            var packageSourcesToBeRemoved = new List<PackageSource>();

            // doing migration
            for (var i = 0; i < loadedPackageSources.Count; i++)
            {
                var ps = loadedPackageSources[i];
                PackageSource targetPackageSource;
                if (_migratePackageSources.TryGetValue(ps, out targetPackageSource))
                {
                    if (loadedPackageSources.Any(p => p.Equals(targetPackageSource)))
                    {
                        packageSourcesToBeRemoved.Add(loadedPackageSources[i]);
                    }
                    else
                    {
                        loadedPackageSources[i] = targetPackageSource.Clone();
                        // make sure we preserve the IsEnabled property when migrating package sources
                        loadedPackageSources[i].IsEnabled = ps.IsEnabled;
                    }
                    hasChanges = true;
                }
            }

            foreach (var packageSource in packageSourcesToBeRemoved)
            {
                loadedPackageSources.Remove(packageSource);
            }

            if (hasChanges)
            {
                SavePackageSources(loadedPackageSources);
            }
        }

        public void SavePackageSources(IEnumerable<PackageSource> sources)
        {
            // clear the old values
            // and write the new ones
            var sourcesToWrite = sources.Where(s => !s.IsMachineWide && s.IsPersistable);

            var sourcesSection = Settings.Sections[ConfigurationConstants.PackageSources];
            var existingSettings = sourcesSection?.Children.Select(c => c as SourceElement).Where(c => c != null && !c.Origin.IsMachineWide).ToList();
            var existingSettingsLookup = existingSettings.ToLookup(setting => setting.Key, StringComparer.OrdinalIgnoreCase);

            var disabledSourcesSection = Settings.Sections[ConfigurationConstants.DisabledPackageSources];
            var existingDisabledSources = disabledSourcesSection.Children.Select(c => c as AddElement).Where(c => c != null);
            var existingDisabledSourcesLookup = existingDisabledSources.ToLookup(setting => setting.Key, StringComparer.OrdinalIgnoreCase);

            var sourceSettings = new List<SourceElement>();
            var sourcesToDisable = new List<AddElement>();

            //foreach (var source in sourcesToWrite)
            //{
            //    var foundSettingWithSourcePriority = false;
            //    var existingSettingForSource = existingSettingsLookup[source.Name];

            //    // Preserve packageSource entries from low priority settings.
            //    foreach (var existingSetting in existingSettingForSource)
            //    {
            //        // Write all settings other than the currently written one to the current NuGet.config.
            //        if (ReadProtocolVersion(existingSetting) == source.ProtocolVersion)
            //        {
            //            // Update the source value of all settings with the same protocol version.
            //            foundSettingWithSourcePriority = true;
            //        }
            //        sourceSettings.Add(existingSetting);
            //    }

            //    if (!foundSettingWithSourcePriority)
            //    {
            //        // This is a new source, add it to the Setting with the lowest priority.
            //        // if there is a clear tag in one config file, new source will be cleared
            //        // we should set new source priority to lowest existingSetting priority
            //        // NOTE: origin can be null here because it isn't ever used when saving.
            //        var settingValue = new SourceElement(source.Name, source.Source);

            //        if (source.ProtocolVersion != PackageSource.DefaultProtocolVersion)
            //        {
            //            settingValue.ProtocolVersion = source.ProtocolVersion.ToString(CultureInfo.InvariantCulture);
            //        }

            //        sourceSettings.Add(settingValue);
            //    }

            //    var existingDisabledSettings = existingDisabledSourcesLookup[source.Name];
            //    // Preserve disabledPackageSource entries from low priority settings.
            //    foreach (var setting in existingDisabledSettings)
            //    {
            //        sourcesToDisable.Add(setting);
            //    }

            //    if (!source.IsEnabled)
            //    {
            //        // Add an entry to the disabledPackageSource in the file that contains
            //        sourcesToDisable.Add(new AddElement(source.Name, "true"));
            //    }
            //}

            //// add entries to the disabledPackageSource for machine wide setting
            //foreach (var source in sources.Where(s => s.IsMachineWide && !s.IsEnabled))
            //{
            //    sourcesToDisable.Add(new AddElement(source.Name, "true", origin: null, isMachineWide: true, priority: 0));
            //}

            //// add entries to the disablePackageSource for disabled package sources that are not in loaded 'sources'
            //foreach (var setting in existingDisabledSources)
            //{
            //    // The following code ensures that we do not miss to mark an existing disabled source as disabled.
            //    // However, ONLY mark an existing disable source setting as disabled, if,
            //    // 1) it is not in the list of loaded package sources, or,
            //    // 2) it is not already in the list of sources to disable.
            //    if (!sources.Any(s => string.Equals(s.Name, setting.Key, StringComparison.OrdinalIgnoreCase)) &&
            //        !sourcesToDisable.Any(s => string.Equals(s.Key, setting.Key, StringComparison.OrdinalIgnoreCase)
            //                                && s.Priority == setting.Priority))
            //    {
            //        sourcesToDisable.Add(setting);
            //    }
            //}

            //// Write the updates to the nearest settings file.
            //Settings.UpdateSections(ConfigurationConstants.PackageSources, sourceSettings);

            //// overwrite new values for the <disabledPackageSources> section
            //Settings.UpdateSections(ConfigurationConstants.DisabledPackageSources, sourcesToDisable);

            //// Overwrite the <packageSourceCredentials> section
            //Settings.DeleteSection(ConfigurationConstants.CredentialsSectionName);

            //foreach (var source in sources.Where(s => s.Credentials != null && s.Credentials.IsValid()))
            //{
            //    Settings.SetNestedValues(
            //        ConfigurationConstants.CredentialsSectionName,
            //        source.Name,
            //        GetCredentialValues(source.Credentials));
            //}

            //// Update/Add trusted sources
            //// Deletion of a trusted source should be done separately using TrustedSourceProvider.DeleteSource()
            //var trustedSources = sources
            //    .Where(s => s.TrustedSource != null)
            //    .Select(s => s.TrustedSource);

            //if (trustedSources.Any())
            //{
            //    _trustedSourceProvider.SaveTrustedSources(trustedSources);
            //}

            OnPackageSourcesChanged();
        }

        /// <summary>
        /// Fires event PackageSourcesChanged
        /// </summary>
        private void OnPackageSourcesChanged()
        {
            PackageSourcesChanged?.Invoke(this, EventArgs.Empty);
        }

        private static KeyValuePair<string, string>[] GetCredentialValues(PackageSourceCredential credentials)
        {
            var passwordToken = credentials.IsPasswordClearText
                ? ConfigurationConstants.ClearTextPasswordToken
                : ConfigurationConstants.PasswordToken;

            return new[]
            {
                new KeyValuePair<string, string>(ConfigurationConstants.UsernameToken, credentials.Username),
                new KeyValuePair<string, string>(passwordToken, credentials.PasswordText)
            };
        }

        public string DefaultPushSource
        {
            get
            {
                var source = SettingsUtility.GetDefaultPushSource(Settings);

                if (string.IsNullOrEmpty(source))
                {
                    source = ConfigurationDefaults.Instance.DefaultPushSource;
                }

                return source;
            }
        }

        public void DisablePackageSource(PackageSource source)
        {
            if (source == null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            var newElement = new AddElement(source.Name, "true");

            if (!Settings.Sections.ContainsKey(ConfigurationConstants.DisabledPackageSources) && !Settings.TryCreateSection(ConfigurationConstants.DisabledPackageSources))
            {
                throw new InvalidOperationException("could not add disabled package sources section");
            }

            var disabledPackageSourcesSection = Settings.Sections[ConfigurationConstants.DisabledPackageSources];

            if (disabledPackageSourcesSection.TryAddChild(newElement))
            {
                throw new InvalidOperationException("could not add disabled package source");
            }
        }

        public bool IsPackageSourceEnabled(PackageSource source)
        {
            if (source == null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            var disabledSources = Settings.Sections[ConfigurationConstants.DisabledPackageSources];
            var value = disabledSources?.GetChildElement(ConfigurationConstants.KeyAttribute, source.Name);

            // It doesn't matter what value it is.
            // As long as the package source name is persisted in the <disabledPackageSources> section, the source is disabled.
            return value == null;
        }

        /// <summary>
        /// Gets the name of the ActivePackageSource from NuGet.Config
        /// </summary>
        public string ActivePackageSourceName
        {
            get
            {
                var activeSourceSection = Settings.Sections[ConfigurationConstants.ActivePackageSourceSectionName];
                var activeSource = activeSourceSection?.Children.Select(c => c as AddElement).Where(c => c != null).FirstOrDefault();
                if (activeSource == null)
                {
                    return null;
                }

                return activeSource.Key;
            }
        }

        /// <summary>
        /// Saves the <paramref name="source" /> as the active source.
        /// </summary>
        /// <param name="source"></param>
        public void SaveActivePackageSource(PackageSource source)
        {
            try
            {
                var activePackageSource = new AddElement(source.Name, source.Source);
                var activePackageSourceSection = Settings.Sections[ConfigurationConstants.ActivePackageSourceSectionName];

                if (activePackageSourceSection != null)
                {
                    activePackageSourceSection.TryRemove();
                }

                if (Settings.TryCreateSection(ConfigurationConstants.ActivePackageSourceSectionName))
                {
                    activePackageSourceSection = Settings.Sections[ConfigurationConstants.ActivePackageSourceSectionName];
                    activePackageSourceSection.TryAddChild(activePackageSource);
                }
            }
            catch (Exception)
            {
                // we want to ignore all errors here.
            }
        }

        private class IndexedPackageSource
        {
            public int Index { get; set; }

            public PackageSource PackageSource { get; set; }
        }

        public event EventHandler PackageSourcesChanged;
    }
}
