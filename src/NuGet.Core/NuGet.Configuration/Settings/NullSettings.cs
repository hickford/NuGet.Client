// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Globalization;

namespace NuGet.Configuration
{
    public class NullSettings : ISettings
    {
        public event EventHandler SettingsChanged = delegate { };

        public static NullSettings Instance { get; } = new NullSettings();

        public Dictionary<string, SettingSection> Sections => new Dictionary<string, SettingSection>();

        public bool TryCreateSection(string sectionName)
        {
            throw new InvalidOperationException(string.Format(CultureInfo.CurrentCulture, Resources.InvalidNullSettingsOperation, nameof(TryCreateSection)));
        }

        public IEnumerable<string> GetConfigFilePaths()
        {
            return new List<string>();
        }

        public IEnumerable<string> GetConfigRoots()
        {
            return new List<string>();
        }
    }
}
