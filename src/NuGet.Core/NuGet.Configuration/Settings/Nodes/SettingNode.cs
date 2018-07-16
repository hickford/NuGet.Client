// Copyright(c) .NET Foundation.All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Xml.Linq;

namespace NuGet.Configuration
{
    public abstract class SettingNode : IEquatable<SettingNode>, ISettingNode
    {
        protected XNode Node { get; set; }

        internal ISettingContainer Parent { get; set; }

        internal ISettingsFile Origin { get; set; }

        protected SettingNode()
        {
        }

        internal SettingNode(XNode node, ISettingContainer parent, ISettingsFile origin)
        {
            Node = node ?? throw new ArgumentNullException(nameof(node));
            Parent = parent ?? throw new ArgumentNullException(nameof(parent));
            Origin = origin ?? throw new ArgumentNullException(nameof(origin));
        }

        public abstract bool Equals(SettingNode other);

        public abstract XNode AsXNode();

        public bool TryRemove()
        {
            if (Parent != null)
            {
                return Parent.TryRemoveChild(this);
            }

            return false;
        }

        internal virtual void AddParent(ISettingContainer parent)
        {
            if (parent.Origin == null)
            {
                throw new ArgumentException();
            }

            Parent = parent ?? throw new ArgumentNullException(nameof(parent));
            Origin = parent.Origin;
            Node = AsXNode();
        }

        internal virtual void AddParent(ISettingContainer parent, ISettingsFile origin)
        {
            if (parent.Origin != null)
            {
                throw new ArgumentException();
            }

            Parent = parent ?? throw new ArgumentNullException(nameof(parent));
            Origin = origin ?? throw new ArgumentNullException(nameof(origin));
            Node = AsXNode();
        }
    }
}
