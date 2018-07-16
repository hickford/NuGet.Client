// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Xml;
using NuGet.Common;

namespace NuGet.Configuration
{
    /// <summary>
    /// Concrete implementation of ISettings to support NuGet Settings
    /// </summary>
    public class Settings : ISettings
    {
        /// <summary>
        /// Default file name for a settings file is 'NuGet.config'
        /// Also, the user level setting file at '%APPDATA%\NuGet' always uses this name
        /// </summary>
        public static readonly string DefaultSettingsFileName = "NuGet.Config";

        /// <summary>
        /// NuGet config names with casing ordered by precedence.
        /// </summary>
        public static readonly string[] OrderedSettingsFileNames =
            PathUtility.IsFileSystemCaseInsensitive ?
            new[] { DefaultSettingsFileName } :
            new[]
            {
                "nuget.config", // preferred style
                "NuGet.config", // Alternative
                DefaultSettingsFileName  // NuGet v2 style
            };

        public static readonly string[] SupportedMachineWideConfigExtension =
            RuntimeEnvironmentHelper.IsWindows ?
            new[] { "*.config" } :
            new[] { "*.Config", "*.config" };

        private bool Cleared { get; set; }

        private SettingsFile _settingsHead { get; }
        private NuGetConfiguration _computedSettings { get; set; }

        // We want to write anything new to the config closest to the user
        private ISettingsFile _defaultOutputSettingsFile => Priority.LastOrDefault(s => !s.IsMachineWide);

        public Dictionary<string, SettingSection> Sections => _computedSettings.Sections;
        
        public event EventHandler SettingsChanged = delegate { };

        internal Settings(SettingsFile settingsHead)
        {
            _settingsHead = settingsHead;

            var curr = _settingsHead.Next;
            while (curr != null)
            {
                curr.SettingsChanged += (_, __) => { RefreshSettingValues(); };
                curr = curr.Next;
            }

            MergeSettingFilesValues();
        }

        private void RefreshSettingValues()
        {
            MergeSettingFilesValues();
            SettingsChanged?.Invoke(this, EventArgs.Empty);
        }

        private void MergeSettingFilesValues()
        {
            var settings = new NuGetConfiguration(_settingsHead.RootElement, _defaultOutputSettingsFile);

            var curr = _settingsHead.Next;
            while (curr != null)
            {
                settings.Merge(curr.RootElement);
                curr = curr.Next;
            }

            _computedSettings = settings;
        }

        /// <summary>
        /// Enumerates the sequence of <see cref="ISettingsFile"/> instances
        /// used in the order they where read
        /// </summary>
        internal IEnumerable<ISettingsFile> Priority
        {
            get
            {
                // explore the linked list, terminating when a duplicate path is found
                var current = _settingsHead;
                var found = new List<SettingsFile>();
                var paths = new HashSet<string>();
                while (current != null && paths.Add(current.ConfigFilePath))
                {
                    found.Add(current);
                    current = current.Next;
                }

                return found;
            }
        }

        public IEnumerable<string> GetConfigFilePaths()
        {
            return Priority.Select(config => Path.GetFullPath(Path.Combine(config.Root, config.FileName)));
        }

        public IEnumerable<string> GetConfigRoots()
        {
            return Priority.Select(config => config.Root);
        }

        /// <summary>
        /// Load default settings based on a directory.
        /// This includes machine wide settings.
        /// </summary>
        public static ISettings LoadDefaultSettings(string root)
        {
            return LoadSettings(
                root,
                configFileName: null,
                machineWideSettings: new XPlatMachineWideSetting(),
                loadUserWideSettings: true,
                useTestingGlobalPath: false);
        }

        /// <summary>
        /// Loads user settings from the NuGet configuration files. The method walks the directory
        /// tree in <paramref name="root" /> up to its root, and reads each NuGet.config file
        /// it finds in the directories. It then reads the user specific settings,
        /// which is file <paramref name="configFileName" />
        /// in <paramref name="root" /> if <paramref name="configFileName" /> is not null,
        /// If <paramref name="configFileName" /> is null, the user specific settings file is
        /// %AppData%\NuGet\NuGet.config.
        /// After that, the machine wide settings files are added.
        /// </summary>
        /// <remarks>
        /// For example, if <paramref name="root" /> is c:\dir1\dir2, <paramref name="configFileName" />
        /// is "userConfig.file", the files loaded are (in the order that they are loaded):
        /// c:\dir1\dir2\nuget.config
        /// c:\dir1\nuget.config
        /// c:\nuget.config
        /// c:\dir1\dir2\userConfig.file
        /// machine wide settings (e.g. c:\programdata\NuGet\Config\*.config)
        /// </remarks>
        /// <param name="root">
        /// The file system to walk to find configuration files.
        /// Can be null.
        /// </param>
        /// <param name="configFileName">The user specified configuration file.</param>
        /// <param name="machineWideSettings">
        /// The machine wide settings. If it's not null, the
        /// settings files in the machine wide settings are added after the user sepcific
        /// config file.
        /// </param>
        /// <returns>The settings object loaded.</returns>
        public static ISettings LoadDefaultSettings(
            string root,
            string configFileName,
            IMachineWideSettings machineWideSettings)
        {
            return LoadSettings(
                root,
                configFileName,
                machineWideSettings,
                loadUserWideSettings: true,
                useTestingGlobalPath: false);
        }

        /// <summary>
        /// Loads Specific NuGet.Config file. The method only loads specific config file 
        /// which is file <paramref name="configFileName"/>from <paramref name="root"/>.
        /// </summary>
        public static ISettings LoadSpecificSettings(string root, string configFileName)
        {
            if (string.IsNullOrEmpty(configFileName))
            {
                throw new ArgumentException(Resources.Argument_Cannot_Be_Null_Or_Empty, nameof(configFileName));
            }

            return LoadSettings(
                root,
                configFileName,
                machineWideSettings: null,
                loadUserWideSettings: true,
                useTestingGlobalPath: false);
        }

        /// <summary>
        /// For internal use only
        /// </summary>
        public static ISettings LoadSettings(
            string root,
            string configFileName,
            IMachineWideSettings machineWideSettings,
            bool loadUserWideSettings,
            bool useTestingGlobalPath)
        {
            // Walk up the tree to find a config file; also look in .nuget subdirectories
            // If a configFile is passed, don't walk up the tree. Only use that single config file.
            var validSettingFiles = new List<SettingsFile>();
            if (root != null && string.IsNullOrEmpty(configFileName))
            {
                validSettingFiles.AddRange(
                    GetSettingsFilesFullPath(root)
                        .Select(f => ReadSettings(root, f))
                        .Where(f => f != null));
            }

            return LoadSettingsForSpecificConfigs(
                root,
                configFileName,
                validSettingFiles,
                machineWideSettings,
                loadUserWideSettings,
                useTestingGlobalPath);
        }

        public static ISettings LoadSettingsGivenConfigPaths(IList<string> configFilePaths)
        {
            var settings = new List<SettingsFile>();
            if (configFilePaths == null || configFilePaths.Count == 0)
            {
                return NullSettings.Instance;
            }

            foreach (var configFile in configFilePaths)
            {
                var file = new FileInfo(configFile);
                settings.Add(new SettingsFile(file.DirectoryName, file.Name));
            }

            return LoadSettingsForSpecificConfigs(
                settings.First().Root,
                settings.First().FileName,
                validSettingFiles: settings,
                machineWideSettings: null,
                loadUserWideSettings: false,
                useTestingGlobalPath: false);
        }

        internal static ISettings LoadSettingsForSpecificConfigs(
            string root,
            string configFileName,
            List<SettingsFile> validSettingFiles,
            IMachineWideSettings machineWideSettings,
            bool loadUserWideSettings,
            bool useTestingGlobalPath)
        {
            if (loadUserWideSettings)
            {
                LoadUserSpecificSettings(validSettingFiles, root, configFileName, machineWideSettings, useTestingGlobalPath);
            }

            if (machineWideSettings != null && string.IsNullOrEmpty(configFileName))
            {
                validSettingFiles.AddRange(
                    machineWideSettings.Settings.Priority.Select(
                        s => new SettingsFile(s.Root, s.FileName, s.IsMachineWide)));
            }

            if (validSettingFiles?.Any() != true)
            {
                // This means we've failed to load all config files and also failed to load or create the one in %AppData%
                // Work Item 1531: If the config file is malformed and the constructor throws, NuGet fails to load in VS.
                // Returning a null instance prevents us from silently failing and also from picking up the wrong config
                return NullSettings.Instance;
            }

            //SetClearTagForSettings(validSettingFiles);

            SettingsFile.ConnectSettingsFilesLinkedList(validSettingFiles);

            // Create a settings object with the linked list head. Typically, it's either the config file in %ProgramData%\NuGet\Config,
            // or the user wide config (%APPDATA%\NuGet\nuget.config) if there are no machine
            // wide config files. The head file is the one we want to read first, while the user wide config
            // is the one that we want to write to.
            // TODO: add UI to allow specifying which one to write to
            return new Settings(validSettingFiles.Last());
        }

        private static void LoadUserSpecificSettings(
            List<SettingsFile> validSettingFiles,
            string root,
            string configFileName,
            IMachineWideSettings machineWideSettings,
            bool useTestingGlobalPath)
        {
            // Path.Combine is performed with root so it should not be null
            // However, it is legal for it be empty in this method
            var rootDirectory = root ?? string.Empty;

            // for the default location, allow case where file does not exist, in which case it'll end
            // up being created if needed
            SettingsFile userSpecificSettings = null;
            if (configFileName == null)
            {
                var defaultSettingsFilePath = string.Empty;
                if (useTestingGlobalPath)
                {
                    defaultSettingsFilePath = Path.Combine(rootDirectory, "TestingGlobalPath", DefaultSettingsFileName);
                }
                else
                {
                    var userSettingsDir = NuGetEnvironment.GetFolderPath(NuGetFolderPath.UserSettingsDirectory);

                    // If there is no user settings directory, return no settings
                    if (userSettingsDir == null)
                    {
                        return;
                    }
                    defaultSettingsFilePath = Path.Combine(userSettingsDir, DefaultSettingsFileName);
                }

                if (!File.Exists(defaultSettingsFilePath) && machineWideSettings != null)
                {
                    // Since defaultSettingsFilePath is a full path, so it doesn't matter what value is
                    // used as root for the PhysicalFileSystem.
                    userSpecificSettings = ReadSettings(
                    rootDirectory,
                    defaultSettingsFilePath);

                    //// TODO-PATO: should this be deleted?
                    //// ***********************************
                    //// Disable machinewide sources to improve perf
                    //var disabledSources = new List<SettingValue>();
                    //foreach (var setting in machineWideSettings.Settings)
                    //{
                    //    var values = setting.GetSettingValues(ConfigurationConstants.PackageSources, isPath: true);
                    //    foreach (var value in values)
                    //    {
                    //        var packageSource = new PackageSource(value.Value);

                    //        // if the machine wide package source is http source, disable it by default
                    //        if (packageSource.IsHttp)
                    //        {
                    //            disabledSources.Add(new SettingValue(value.Key, "true", origin: setting, isMachineWide: true, priority: 0));
                    //        }
                    //    }
                    //}
                    //userSpecificSettings.UpdateSections(ConfigurationConstants.DisabledPackageSources, disabledSources);
                    //// ***********************************

                }
                else
                {
                    userSpecificSettings = ReadSettings(rootDirectory, defaultSettingsFilePath);
                    var IsEmptyConfig = userSpecificSettings.IsEmpty();

                    if (IsEmptyConfig)
                    {
                        var trackFilePath = Path.Combine(Path.GetDirectoryName(defaultSettingsFilePath), NuGetConstants.AddV3TrackFile);

                        if (!File.Exists(trackFilePath))
                        {
                            File.Create(trackFilePath).Dispose();
                            //// TODO-PATO: should this be deleted?
                            //// ***********************************
                            //var defaultPackageSource = new SettingValue(NuGetConstants.FeedName, NuGetConstants.V3FeedUrl, isMachineWide: false);
                            //defaultPackageSource.AdditionalData.Add(ConfigurationConstants.ProtocolVersionAttribute, "3");
                            //userSpecificSettings.UpdateSections(ConfigurationConstants.PackageSources, new List<SettingValue> { defaultPackageSource });
                            //// ***********************************
                        }
                    }
                }
            }
            else
            {
                if (!FileSystemUtility.DoesFileExistIn(rootDirectory, configFileName))
                {
                    var message = string.Format(CultureInfo.CurrentCulture,
                        Resources.FileDoesNotExist,
                        Path.Combine(rootDirectory, configFileName));
                    throw new InvalidOperationException(message);
                }

                userSpecificSettings = ReadSettings(rootDirectory, configFileName);
            }

            if (userSpecificSettings != null)
            {
                validSettingFiles.Add(userSpecificSettings);
            }
        }

        /// <summary>
        /// Loads the machine wide settings.
        /// </summary>
        /// <remarks>
        /// For example, if <paramref name="paths" /> is {"IDE", "Version", "SKU" }, then
        /// the files loaded are (in the order that they are loaded):
        /// %programdata%\NuGet\Config\IDE\Version\SKU\*.config
        /// %programdata%\NuGet\Config\IDE\Version\*.config
        /// %programdata%\NuGet\Config\IDE\*.config
        /// %programdata%\NuGet\Config\*.config
        /// </remarks>
        /// <param name="root">The file system in which the settings files are read.</param>
        /// <param name="paths">The additional paths under which to look for settings files.</param>
        /// <returns>The list of settings read.</returns>
        public static Settings LoadMachineWideSettings(
            string root,
            params string[] paths)
        {
            if (string.IsNullOrEmpty(root))
            {
                throw new ArgumentException("root cannot be null or empty");
            }

            var settingFiles = new List<SettingsFile>();
            var combinedPath = Path.Combine(paths);

            while (true)
            {
                // load setting files in directory
                foreach (var file in FileSystemUtility.GetFilesRelativeToRoot(root, combinedPath, SupportedMachineWideConfigExtension, SearchOption.TopDirectoryOnly))
                {
                    var settings = ReadSettings(root, file, isMachineWideSettings: true);
                    if (settings != null)
                    {
                        settingFiles.Add(settings);
                    }
                }

                if (combinedPath.Length == 0)
                {
                    break;
                }

                var index = combinedPath.LastIndexOf(Path.DirectorySeparatorChar);
                if (index < 0)
                {
                    index = 0;
                }
                combinedPath = combinedPath.Substring(0, index);
            }

            SettingsFile.ConnectSettingsFilesLinkedList(settingFiles);

            return new Settings(settingFiles.Last());
        }

        public bool TryCreateSection(string sectionName)
        {
            var section = new SettingSection(sectionName);

            if (_defaultOutputSettingsFile.RootElement.TryAddChild(section))
            {
                return true;
            }

            return false;
        }

        public static string ApplyEnvironmentTransform(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return value;
            }

            return Environment.ExpandEnvironmentVariables(value);
        }

        internal static string ResolveRelativePath(ISettingsFile originConfig, string path)
        {
            if (originConfig == null)
            {
                return null;
            }

            if (string.IsNullOrEmpty(path))
            {
                return path;
            }

            return Path.Combine(originConfig.Root, ResolvePath(Path.GetDirectoryName(originConfig.ConfigFilePath), path));
        }

        private static string ResolvePath(string configDirectory, string value)
        {
            // Three cases for when Path.IsRooted(value) is true:
            // 1- C:\folder\file
            // 2- \\share\folder\file
            // 3- \folder\file
            // In the first two cases, we want to honor the fully qualified path
            // In the last case, we want to return X:\folder\file with X: drive where config file is located.
            // However, Path.Combine(path1, path2) always returns path2 when Path.IsRooted(path2) == true (which is current case)
            var root = Path.GetPathRoot(value);
            // this corresponds to 3rd case
            if (root != null
                && root.Length == 1
                && (root[0] == Path.DirectorySeparatorChar || value[0] == Path.AltDirectorySeparatorChar))
            {
                return Path.Combine(Path.GetPathRoot(configDirectory), value.Substring(1));
            }
            return Path.Combine(configDirectory, value);
        }

        private static SettingsFile ReadSettings(string settingsRoot, string settingsPath, bool isMachineWideSettings = false)
        {
            try
            {
                var tuple = GetFileNameAndItsRoot(settingsRoot, settingsPath);
                var filename = tuple.Item1;
                var root = tuple.Item2;
                return new SettingsFile(root, filename, isMachineWideSettings);
            }
            catch (XmlException)
            {
                return null;
            }
        }

        public static Tuple<string, string> GetFileNameAndItsRoot(string root, string settingsPath)
        {
            string fileName = null;
            string directory = null;

            if (Path.IsPathRooted(settingsPath))
            {
                fileName = Path.GetFileName(settingsPath);
                directory = Path.GetDirectoryName(settingsPath);
            }
            else if (!FileSystemUtility.IsPathAFile(settingsPath))
            {
                var fullPath = Path.Combine(root ?? string.Empty, settingsPath);
                fileName = Path.GetFileName(fullPath);
                directory = Path.GetDirectoryName(fullPath);
            }
            else
            {
                fileName = settingsPath;
                directory = root;
            }

            return new Tuple<string, string>(fileName, directory);
        }

        /// <remarks>
        /// Order is most significant (e.g. applied last) to least significant (applied first)
        /// ex:
        /// c:\someLocation\nuget.config
        /// c:\nuget.config
        /// </remarks>
        private static IEnumerable<string> GetSettingsFilesFullPath(string root)
        {
            // for dirs obtained by walking up the tree, only consider setting files that already exist.
            // otherwise we'd end up creating them.
            foreach (var dir in GetSettingsFilePaths(root))
            {
                var fileName = GetSettingsFileNameFromDir(dir);
                if (fileName != null)
                {
                    yield return fileName;
                }
            }

            yield break;
        }

        /// <summary>
        /// Checks for each possible casing of nuget.config in the directory. The first match is
        /// returned. If there are no nuget.config files null is returned.
        /// </summary>
        /// <remarks>For windows <see cref="OrderedSettingsFileNames"/> contains a single casing since
        /// the file system is case insensitive.</remarks>
        private static string GetSettingsFileNameFromDir(string directory)
        {
            foreach (var nugetConfigCasing in OrderedSettingsFileNames)
            {
                var file = Path.Combine(directory, nugetConfigCasing);
                if (File.Exists(file))
                {
                    return file;
                }
            }

            return null;
        }

        private static IEnumerable<string> GetSettingsFilePaths(string root)
        {
            while (root != null)
            {
                yield return root;
                root = Path.GetDirectoryName(root);
            }

            yield break;
        }
    }
}
