// Copyright(c) .NET Foundation.All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;

namespace NuGet.Configuration
{
    public abstract class SettingElement : SettingNode, ISettingContainer
    {
        public abstract string Name { get; }

        /// <summary>
        /// 
        /// </summary>
        /// <remarks>This property should never be null</remarks>
        protected IDictionary<string, string> Attributes { get; }

        /// <summary>
        /// 
        /// </summary>
        /// <remarks>This property should never be null</remarks>
        protected HashSet<SettingNode> ChildrenSet { get; set; }

        public IReadOnlyCollection<SettingNode> Children => ChildrenSet.ToList();

        public abstract bool CanBeCleared { get; }

        ISettingsFile ISettingContainer.Origin => Origin;

        /// <summary>
        /// Default constructor
        /// </summary>
        protected SettingElement()
        {
            Attributes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            ChildrenSet = new HashSet<SettingNode>();
        }

        /// <summary>
        /// Constructor used when element is read from a file
        /// </summary>
        /// <param name="element">Xelement read from XML file document tree</param>
        /// <param name="parent">Container that holds this element as a child</param>
        /// <param name="origin">Settings file that this element was read from</param>
        internal SettingElement(XElement element, ISettingContainer parent, ISettingsFile origin)
            : base(element, parent, origin)
        {
            ChildrenSet = SettingFactory.ParseChildren(this, element, origin);

            Attributes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var existingAttribute in element.Attributes())
            {
                Attributes.Add(existingAttribute.Name.LocalName, existingAttribute.Value);
            }
        }

        internal override void AddParent(ISettingContainer parent)
        {
            if (parent.Origin == null)
            {
                throw new ArgumentException();
            }

            Parent = parent ?? throw new ArgumentNullException(nameof(parent));
            Origin = parent.Origin;
            Node = AsXNode();

            foreach (var child in ChildrenSet)
            {
                if (child.Origin == null)
                {
                    child.AddParent(this);
                }
            }
        }

        internal override void AddParent(ISettingContainer parent, ISettingsFile origin)
        {
            if (parent.Origin != null)
            {
                throw new ArgumentException();
            }

            Parent = parent ?? throw new ArgumentNullException(nameof(parent));
            Origin = origin ?? throw new ArgumentNullException(nameof(origin));
            Node = AsXNode();

            foreach (var child in ChildrenSet)
            {
                if (child.Origin == null)
                {
                    child.AddParent(this);
                }
            }
        }

        /// <summary>
        /// Gets a SettingElement child where the  value with the given key
        /// matches the provided value
        /// </summary>
        /// <param name="key">Key to match child elements</param>
        /// <param name="value">Value to match child elements in the given key</param>
        /// <returns>SettingElement for a child that matches the value in the given key</returns>
        public SettingElement GetChildElement(string key, string value)
        {
            foreach (var child in ChildrenSet)
            {
                if (child is SettingElement el && string.Equals(el.Attributes[key], value, StringComparison.Ordinal))
                {
                    return el;
                }
            }

            return null;
        }

        public override XNode AsXNode()
        {
            if (Node != null && Node is XElement xElement)
            {
                return xElement;
            }

            var element = new XElement(Name);
            foreach (var attr in Attributes)
            {
                element.SetAttributeValue(attr.Key, attr.Value);
            }

            foreach (var child in ChildrenSet)
            {
                element.Add(child.AsXNode());
            }

            return element;
        }

        public virtual bool IsEmpty()
        {
            return !ChildrenSet.Any() && !Attributes.Any();
        }

        internal bool TryGetAttributeValue(string key, out string value)
        {
            return Attributes.TryGetValue(key, out value);
        }

        internal bool TryUpdateAttributeValue(string key, string newValue)
        {
            if (string.IsNullOrEmpty(newValue))
            {
                throw new ArgumentNullException(nameof(newValue));
            }

            if (Origin != null && Origin.IsMachineWide)
            {
                return false;
            }

            if (Attributes.ContainsKey(key))
            {
                if (Node != null && Node is XElement xElement)
                {
                    xElement.SetAttributeValue(key, newValue);
                    Origin.Save();
                }

                Attributes[key] = newValue;
                return true;
            }

            return false;
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

            if (ChildrenSet.Contains(child) && ChildrenSet.Remove(child))
            {
                if (child.Origin != null)
                {
                    XElementUtility.RemoveIndented(child.AsXNode());
                    child.Origin.Save();
                }

                if (IsEmpty() && Parent != null)
                {
                    return Parent.TryRemoveChild(this);
                }

                return true;
            }

            return false;
        }

        public bool TryAddChild(SettingNode child)
        {
            if (child == null)
            {
                throw new ArgumentNullException(nameof(child));
            }

            if (Origin != null && Origin.IsMachineWide)
            {
                return false;
            }

            if (!ChildrenSet.Contains(child) && ChildrenSet.Add(child))
            {
                if (Origin != null)
                {
                    child.AddParent(this);
                    XElementUtility.AddIndented(Node as XElement, child.AsXNode());
                    Origin.Save();
                }

                return true;
            }

            return false;
        }
    }
}
