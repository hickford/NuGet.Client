// Copyright(c) .NET Foundation.All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Xml.Linq;

namespace NuGet.Configuration
{
    public sealed class SettingTextContent : SettingNode
    {
        public string Value { get; private set; }

        public SettingTextContent(string value)
            : base()
        {
            Value = value;
        }

        internal SettingTextContent(XText text, ISettingContainer parent, ISettingsFile origin)
            : base(text, parent, origin)
        {
            Value = text.Value;
        }

        public bool Equals(SettingTextContent other)
        {
            if (other == null)
            {
                return false;
            }

            if (ReferenceEquals(this, other))
            {
                return true;
            }

            return string.Equals(Value, other.Value, StringComparison.Ordinal);
        }

        public bool TryUpdate(string newValue)
        {
            if (string.IsNullOrEmpty(newValue))
            {
                throw new ArgumentNullException(nameof(newValue));
            }

            if (Origin != null && Origin.IsMachineWide)
            {
                return false;
            }

            if (Node != null && Node is XText xText)
            {
                xText.Value = newValue;
                Origin.Save();
            }

            Value = newValue;

            return true;
        }

        public override XNode AsXNode()
        {
            if (Node != null && Node is XText xText)
            {
                return xText;
            }

            return new XText(Value);
        }

        public override bool Equals(SettingNode other)
        {
            return Equals(other as SettingTextContent);
        }

        public override bool Equals(object other)
        {
            return Equals(other as SettingTextContent);
        }

        public override int GetHashCode()
        {
            return Value.GetHashCode();
        }
    }
}
