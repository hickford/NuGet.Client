// Copyright(c) .NET Foundation.All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Xml.Linq;

namespace NuGet.Configuration
{
    public sealed class SourceElement : AddElement
    {
        public override string Value => Settings.ResolveRelativePath(Origin, Settings.ApplyEnvironmentTransform(Attributes[ConfigurationConstants.ValueAttribute]));

        public string ProtocolVersion
        {
            get => Settings.ApplyEnvironmentTransform(Attributes[ConfigurationConstants.ProtocolVersionAttribute]);
            set => Attributes[ConfigurationConstants.ProtocolVersionAttribute] = value;
        }

        public SourceElement(string key, string value)
            : this(key, value, protocolVersion: "")
        {
        }

        internal SourceElement(XElement element, ISettingContainer parent, ISettingsFile origin)
            : base (element, parent, origin)
        {
        }

        public SourceElement(string key, string value, string protocolVersion)
            : base(key, value)
        {
            if (!string.IsNullOrEmpty(protocolVersion))
            {
                ProtocolVersion = protocolVersion;
            }
        }
    }
}
