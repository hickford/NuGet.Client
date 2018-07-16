// Copyright(c) .NET Foundation.All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Xml.Linq;

namespace NuGet.Configuration
{
    public sealed class ClearElement : SettingElement, IEquatable<ClearElement>
    {
        public override string Name => ConfigurationConstants.Clear;

        public override bool CanBeCleared => false;

        public ClearElement()
        {
        }

        internal ClearElement(XElement element, ISettingContainer parent, ISettingsFile origin)
            : base(element, parent, origin)
        {
        }

        public bool Equals(ClearElement other)
        {
            return other != null && ReferenceEquals(this, other);
        }

        public override bool Equals(SettingNode other)
        {
            return Equals(other as ClearElement);
        }

        public override bool Equals(object other)
        {
            return Equals(other as ClearElement);
        }

        public override int GetHashCode()
        {
            return Attributes.GetHashCode();
        }
    }
}
