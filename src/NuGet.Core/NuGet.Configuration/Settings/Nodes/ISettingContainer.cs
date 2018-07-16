// Copyright(c) .NET Foundation.All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGet.Configuration
{
    internal interface ISettingContainer
    {
        string Name { get; }

        ISettingsFile Origin { get; }

        bool TryRemoveChild(SettingNode child);

        bool TryAddChild(SettingNode child);
    }
}

