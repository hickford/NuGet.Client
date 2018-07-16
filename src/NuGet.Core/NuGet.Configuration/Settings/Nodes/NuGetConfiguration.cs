// Copyright(c) .NET Foundation.All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;

namespace NuGet.Configuration
{
    internal sealed class NuGetConfiguration : ISettingNode, ISettingContainer
    {
        public string Name => ConfigurationConstants.Configuration;

        public Dictionary<string, SettingSection> Sections { get; }

        internal ISettingsFile Origin { get; }

        ISettingsFile ISettingContainer.Origin => Origin;

        private readonly XNode _node;

        internal NuGetConfiguration(XElement element, ISettingsFile origin)
        {
            Origin = origin ?? throw new ArgumentNullException(nameof(origin));
            _node = element ?? throw new ArgumentNullException(nameof(element));

            Sections = element.Descendants()?.Select(
                d => SettingFactory.Parse(d, this, origin) as SettingSection
                ).Where(s => s != null).ToDictionary(s => s.Name) ?? new Dictionary<string, SettingSection>();
        }

        /// <summary>
        /// Default config file initilization
        /// </summary>
        public NuGetConfiguration()
        {
            var defaultSource = new SourceElement(NuGetConstants.FeedName, NuGetConstants.V3FeedUrl, protocolVersion: "3");
            Sections = new Dictionary<string, SettingSection>
            {
                { ConfigurationConstants.PackageSources, new SettingSection(ConfigurationConstants.PackageSources, children: new HashSet<SettingNode> { defaultSource }) }
            };
        }

        public NuGetConfiguration(NuGetConfiguration instance, ISettingsFile origin)
            : this(instance)
        {
            Origin = origin ?? throw new ArgumentNullException(nameof(origin));
        }

        public NuGetConfiguration(NuGetConfiguration instance)
        {
            if (instance == null)
            {
                throw new ArgumentNullException(nameof(instance));
            }

            Sections = new Dictionary<string, SettingSection>(instance.Sections);
        }

        public NuGetConfiguration(IEnumerable<SettingSection> sections)
        {
            if (sections == null)
            {
                throw new ArgumentNullException(nameof(sections));
            }

            Sections = sections.Where(s => s != null).ToDictionary(s => s.Name) ?? new Dictionary<string, SettingSection>();
        }

        public XNode AsXNode()
        {
            if (_node != null)
            {
                return _node;
            }

            var element = new XElement(Name);

            foreach (var section in Sections)
            {
                element.Add(section.Value.AsXNode());
            }

            return element;
        }

        public bool IsEmpty()
        {
            return Sections == null || !Sections.Any() || Sections.All(s => s.Value.IsEmpty());
        }

        public void Merge(NuGetConfiguration other)
        {
            foreach (var section in Sections)
            {
                if (other.Sections.TryGetValue(section.Key, out var settingSection))
                {
                    section.Value.Merge(settingSection);
                }
            }

            foreach (var section in other.Sections)
            {
                if (!Sections.ContainsKey(section.Key))
                {
                    Sections.Add(section.Key, section.Value);
                }
            }
        }

        public bool TryRemoveChild(SettingNode child)
        {
            if (child == null)
            {
                throw new ArgumentNullException(nameof(child));
            }

            if (Origin != null && Origin.IsMachineWide)
            {
                return false;
            }

            if (child is SettingSection section)
            {
                Sections.Remove(section.Name);

                if (child.Origin != null)
                {
                    XElementUtility.RemoveIndented(child.AsXNode());
                    child.Origin.Save();
                }

                return true;
            }

            return false;
        }

        public bool TryRemoveSection(string sectionName)
        {
            if (sectionName == null)
            {
                throw new ArgumentNullException(nameof(sectionName));
            }

            if (Origin != null && Origin.IsMachineWide)
            {
                return false;
            }

            if (Sections.TryGetValue(sectionName, out var section))
            {
                Sections.Remove(sectionName);

                if (section.Origin != null)
                {
                    XElementUtility.RemoveIndented(section.AsXNode());
                    section.Origin.Save();
                }

                return true;
            }

            return false;
        }

        /// <summary>
        /// Adds a known section to the nuget configuration.
        /// This method should only be used to add a section to a NuGetConfiguration specific to a SettingsFile.
        /// </summary>
        /// <param name="child">section to be added</param>
        /// <remarks>The NuGetConfiguration has to have an origin.</remarks>
        /// <returns>true if it succeded adding the section.</returns>
        public bool TryAddChild(SettingNode child)
        {
            if (child == null)
            {
                throw new ArgumentNullException(nameof(child));
            }

            if (Origin == null)
            {
                throw new InvalidOperationException();
            }

            if (Origin.IsMachineWide)
            {
                return false;
            }

            if (child is SettingSection section)
            {
                if (Sections.ContainsKey(section.Name))
                {
                    return false;
                }

                Sections.Add(section.Name, section);
                child.AddParent(this);
                XElementUtility.AddIndented(_node as XElement, child.AsXNode());
                Origin.Save();

                return true;
            }

            return false;
        }

        /// <summary>
        /// Adds a known section to the nuget configuration.
        /// This method should only be used to add a section to the computed NuGetConfiguration.
        /// </summary>
        /// <param name="child">section to be added</param>
        /// <param name="origin">ISettingsFile where the section is going to be added</param>
        /// <remarks>The NuGetConfiguration's origin has to be null.</remarks>
        /// <returns>true if it succeded adding the section.</returns>
        public bool TryAddChild(SettingNode child, ISettingsFile origin)
        {
            if (child == null)
            {
                throw new ArgumentNullException(nameof(child));
            }

            if (Origin != null)
            {
                throw new InvalidOperationException();
            }

            if (child is SettingSection section)
            {
                if (Sections.ContainsKey(section.Name))
                {
                    return false;
                }

                Sections.Add(section.Name, section);
                child.AddParent(this, origin);

                XElementUtility.AddIndented(_node as XElement, child.AsXNode());
                Origin.Save();

                return true;
            }

            return false;
        }
    }
}
