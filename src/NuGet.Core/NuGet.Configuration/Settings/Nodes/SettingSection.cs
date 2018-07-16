// Copyright(c) .NET Foundation.All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Xml;
using System.Xml.Linq;

namespace NuGet.Configuration
{
    public class SettingSection : SettingElement, IEquatable<SettingSection>
    {
        private static readonly HashSet<string> _clearableSections = new HashSet<string>(ConfigurationConstants.ClearableSections);

        public override string Name { get; }

        public override bool CanBeCleared { get; }

        public SettingSection(SettingSection instance)
            : base()
        {
            if (instance == null)
            {
                throw new ArgumentNullException(nameof(instance));
            }

            Name = XmlConvert.EncodeLocalName(instance.Name);
            ChildrenSet = new HashSet<SettingNode>(instance.ChildrenSet);
            CanBeCleared = _clearableSections.Contains(Name);
        }

        internal SettingSection(XElement element, ISettingContainer parent, ISettingsFile origin)
            : base(element, parent, origin)
        {
            Name = element.Name.LocalName;
            CanBeCleared = _clearableSections.Contains(Name);
        }


        public SettingSection(string name)
            : this(name, children: null)
        {
        }

        public SettingSection(string name, HashSet<SettingNode> children)
            : base()
        {
            Name = XmlConvert.EncodeLocalName(name) ?? throw new ArgumentNullException(nameof(name));
            ChildrenSet = children ?? new HashSet<SettingNode>();
            CanBeCleared = _clearableSections.Contains(Name);
        }

        public SettingSection Merge(SettingSection other)
        {
            if (!Equals(other))
            {
                throw new ArgumentException("cannot merge two different sections");
            }

            if (ChildrenSet == null)
            {
                ChildrenSet = new HashSet<SettingNode>(other.Children);
            }
            else
            {
                SettingsUtility.MergeDescendants(CanBeCleared, ChildrenSet, other.Children);
            }

            return this;
        }

        public bool Equals(SettingSection other)
        {
            if (other == null)
            {
                return false;
            }

            if (ReferenceEquals(this, other))
            {
                return true;
            }

            return string.Equals(Name, other.Name, StringComparison.Ordinal);
        }

        public override bool Equals(SettingNode other)
        {
            return Equals(other as SettingSection);
        }

        public override bool Equals(object other)
        {
            return Equals(other as SettingSection);
        }

        public override int GetHashCode()
        {
            return Name.GetHashCode();
        }
    }
}
