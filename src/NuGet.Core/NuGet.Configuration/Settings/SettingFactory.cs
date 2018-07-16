// Copyright(c) .NET Foundation.All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;

namespace NuGet.Configuration
{
    internal static class SettingFactory
    {
        internal static SettingNode Parse(XNode node, ISettingContainer parent, ISettingsFile origin)
        {
            if (node == null)
            {
                throw new ArgumentNullException(nameof(node));
            }

            if (parent == null)
            {
                throw new ArgumentNullException(nameof(parent));
            }

            if (node is XText textNode)
            {
                return new SettingTextContent(textNode, parent, origin);
            }

            if (node is XElement element)
            {
                if (string.Equals(parent.Name, ConfigurationConstants.PackageSources) && string.Equals(element.Name.LocalName, ConfigurationConstants.Add))
                {
                    return new SourceElement(element, parent, origin);
                }
                else if (string.Equals(element.Name.LocalName, ConfigurationConstants.Add))
                {
                    return new AddElement(element, parent, origin);
                }
                else if (string.Equals(element.Name.LocalName, ConfigurationConstants.Clear))
                {
                    return new ClearElement(element, parent, origin);
                }

                return new SettingSection(element, parent, origin);
            }

            return null;
        }

        internal static HashSet<SettingNode> ParseChildren(SettingElement element, XElement xElement, ISettingsFile origin)
        {
            var children = new HashSet<SettingNode>();

            var descendants = xElement.Descendants().Select(d => Parse(d, element, origin)).Where(c => c != null);
            SettingsUtility.MergeDescendants(element.CanBeCleared, children, descendants);

            return children;
        }
    }
}
