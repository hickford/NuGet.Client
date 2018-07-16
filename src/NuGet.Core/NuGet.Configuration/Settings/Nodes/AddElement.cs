// Copyright(c) .NET Foundation.All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Xml.Linq;
using NuGet.Shared;

namespace NuGet.Configuration
{
    public class AddElement : SettingElement, IEquatable<AddElement>
    {
        public override string Name => ConfigurationConstants.Add;

        public override bool CanBeCleared => false;

        public string Key => Attributes[ConfigurationConstants.KeyAttribute];

        public virtual string Value => Settings.ApplyEnvironmentTransform(Attributes[ConfigurationConstants.ValueAttribute]);

        internal AddElement(XElement element, ISettingContainer parent, ISettingsFile origin)
            : base(element, parent, origin)
        {
        }

        public AddElement(string key, string value)
            : base()
        {
            Attributes.Add(ConfigurationConstants.KeyAttribute, key);
            Attributes.Add(ConfigurationConstants.ValueAttribute, value);
        }

        public bool Equals(AddElement other)
        {
            if (other == null)
            {
                return false;
            }

            if (ReferenceEquals(this, other))
            {
                return true;
            }

            if (other.Attributes.Count == Attributes.Count)
            {
                return Attributes.OrderedEquals(other.Attributes, data => data.Key, StringComparer.OrdinalIgnoreCase);
            }

            return false;
        }

        public override bool Equals(SettingNode other)
        {
            return Equals(other as AddElement);
        }

        public override bool Equals(object other)
        {
            return Equals(other as AddElement);
        }

        public override int GetHashCode()
        {
            return Attributes.GetHashCode();
        }
    }
}
