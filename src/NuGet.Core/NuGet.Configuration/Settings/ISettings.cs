// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;

namespace NuGet.Configuration
{
    /// <summary>
    /// Interface to expose NuGet Settings
    /// </summary>
    public interface ISettings
    {
        Dictionary<string, SettingSection> Sections { get; }

        IEnumerable<string> GetConfigFilePaths();

        IEnumerable<string> GetConfigRoots();

        /// <summary>
        /// Creates an empty section with the given <paramref name="sectionName" />.
        /// </summary>
        /// <param name="sectionName">The name of the section.</param>
        bool TryCreateSection(string sectionName);

        /// <summary>
        /// Event raised when the setting have been changed.
        /// </summary>
        event EventHandler SettingsChanged;
    }
}
