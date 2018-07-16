// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using FluentAssertions;
using Moq;
using NuGet.Common;
using NuGet.Test.Utility;
using Xunit;
namespace NuGet.Configuration.Test
{
    public class SettingsFileTests
    {
        [Fact]
        public void Constructor_WithNullRoot_Throws()
        {
            // Act & Assert
            var ex = Record.Exception(() => new SettingsFile(null));
            Assert.NotNull(ex);
            Assert.IsAssignableFrom<ArgumentNullException>(ex);
        }

        [Fact]
        public void Constructor_ConfigurationPath_Succeds()
        {
            // Arrange
            var configFile = "NuGet.Config";
            using (var mockBaseDirectory = TestDirectory.Create())
            {
                var config = @"
<configuration>
    <SectionName>
        <add key='key1' value='value1' />
        <add key='key2' value='value2' />
    </SectionName>
</configuration>";
                ConfigurationFileTestUtility.CreateConfigurationFile(configFile, mockBaseDirectory, config);

                // Act
                var settings = new SettingsFile(mockBaseDirectory);

                // Assert
                var section = settings.RootElement.Sections["SectionName"];
                Assert.NotNull(section);

                var key1Element = section.GetChildElement("key", "key1") as AddElement;
                var key2Element = section.GetChildElement("key", "key2") as AddElement;
                Assert.NotNull(key1Element);
                Assert.NotNull(key2Element);

                Assert.Equal("value1", key1Element.Value);
                Assert.Equal("value2", key2Element.Value);
            }
        }

        [Fact]
        public void Constructor_WithInvalidRootElement_Throws()
        {
            // Arrange
            var configFile = "NuGet.Config";
            using (var mockBaseDirectory = TestDirectory.Create())
            {
                var config = @"
<notvalid>
    <SectionName>
        <add key='key1' value='value1' />
        <add key='key2' value='value2' />
    </SectionName>
</notvalid>";

                ConfigurationFileTestUtility.CreateConfigurationFile(configFile, mockBaseDirectory, config);

                // Act & Assert
                var ex = Record.Exception(() => new SettingsFile(mockBaseDirectory));
                Assert.NotNull(ex);
                Assert.IsAssignableFrom<NuGetConfigurationException>(ex);
            }
        }

        [Fact]
        public void Constructor_WithMalformedConfig_Throws()
        {
            // Arrange
            var configFile = "NuGet.Config";
            using (var mockBaseDirectory = TestDirectory.Create())
            {
                ConfigurationFileTestUtility.CreateConfigurationFile(configFile, mockBaseDirectory, @"<configuration><sectionName></configuration>");

                // Act & Assert
                var ex = Record.Exception(() => new SettingsFile(mockBaseDirectory));
                Assert.NotNull(ex);
                Assert.IsAssignableFrom<NuGetConfigurationException>(ex);
            }
        }

        [Fact]
        public void Constructor_AddElement_WithInvalidAttributes_Throws()
        {
            // Arrange
            var config = @"
<configuration>
    <SectionName>
        <add Key='key2' Value='value2' />
    </SectionName>
</configuration>";
            var nugetConfigPath = "NuGet.Config";
            using (var mockBaseDirectory = TestDirectory.Create())
            {
                ConfigurationFileTestUtility.CreateConfigurationFile(nugetConfigPath, mockBaseDirectory, config);

                // Act and Assert
                var ex = Record.Exception(() => new SettingsFile(mockBaseDirectory));
                Assert.NotNull(ex);
                Assert.IsAssignableFrom<InvalidDataException>(ex);
                Assert.Equal(string.Format("Unable to parse config file '{0}'.", Path.Combine(mockBaseDirectory, nugetConfigPath)), ex.Message);
            }
        }

        [Fact]
        public void Constructor_AddElement_WithValidAttributesButInvalidValues_Throws()
        {
        }

        [Fact]
        public void Constructor_SourceElement_WithInvalidAttributes_Throws()
        {
        }

        [Fact]
        public void Constructor_ClearElement_WithInvalidAttributes_Throws()
        {
        }

        [Fact]
        public void Constructor_SectionElement_WithInvalidAttributes_Throws()
        {
        }

        [Fact]
        public void Sections_WithNonExistantSection_ReturnsNull()
        {
            // Arrange
            var configFile = "NuGet.Config";
            using (var mockBaseDirectory = TestDirectory.Create())
            {
                ConfigurationFileTestUtility.CreateConfigurationFile(configFile, mockBaseDirectory, @"<configuration></configuration>");
                var settings = new SettingsFile(mockBaseDirectory);

                // Act
                var section = settings.RootElement.Sections["DoesNotExisit"];

                // Assert
                Assert.Null(section);
            }
        }

        [Fact]
        public void GetValues_ExistantSection_UnexistantChild_ReturnsNull()
        {
            // Arrange
            var nugetConfigPath = "NuGet.Config";
            var config = @"
<configuration>
    <SectionName>
        <add key='key1' value='value1' />
        <add key='key2' value='value2' />
    </SectionName>
</configuration>";

            using (var mockBaseDirectory = TestDirectory.Create())
            {
                ConfigurationFileTestUtility.CreateConfigurationFile(nugetConfigPath, mockBaseDirectory, config);
                var settings = new SettingsFile(mockBaseDirectory);

                // Act
                var section = settings.RootElement.Sections["SectionName"];
                Assert.NotNull(section);

                var key3Element = section.GetChildElement("key", "key3");

                // Assert
                Assert.Null(key3Element);
            }
        }

        [Fact]
        public void GetValues_ChildrenOfSection_ReturnsAllChildElements()
        {
            // Arrange
            var nugetConfigPath = "NuGet.Config";
            var config = @"
<configuration>
    <SectionName>
        <add key='key1' value='value1' />
        <add key='key2' value='value2' />
    </SectionName>
</configuration>";

            using (var mockBaseDirectory = TestDirectory.Create())
            {
                ConfigurationFileTestUtility.CreateConfigurationFile(nugetConfigPath, mockBaseDirectory, config);
                var settings = new SettingsFile(mockBaseDirectory);

                // Act
                var section = settings.RootElement.Sections["SectionName"];
                Assert.NotNull(section);
                var children = section.Children;

                // Assert
                Assert.NotNull(children);
                Assert.Equal(2, children.Count);
            }
        }

//        [Fact]
//        public void AddElement_WithAdditionalMetada_ParsedSuccessfully()
//        {
//            // Arrange
//            var nugetConfigPath = "NuGet.Config";
//            var config = @"
//<configuration>
//    <Section>
//        <SubSection>
//            <add key='key1' value='value1' meta1='data1' meta2='data2'/>
//        </SubSection>
//    </Section>
//</configuration>";

//            var expectedSetting = new SettingValue("key1", "value1", false);
//            expectedSetting.AdditionalData.Add("meta1", "data1");
//            expectedSetting.AdditionalData.Add("meta2", "data2");
//            var expectedValues = new List<SettingValue>()
//            {
//                expectedSetting
//            };

//            using (var mockBaseDirectory = TestDirectory.Create())
//            {
//                ConfigurationFileTestUtility.CreateConfigurationFile(nugetConfigPath, mockBaseDirectory, config);
//                var settings = new SettingsFile(mockBaseDirectory);

//                // Act
//                var values = settings.GetNestedSettingValues("Section", "SubSection");

//                // Assert
//                values.Should().NotBeNull();
//                values.Should().BeEquivalentTo(expectedValues);
//            }
//        }

//        [Fact]
//        public void GetValues_ChildrenOfSubSection_ReturnsAllChildElements()
//        {
//            // Arrange
//            var nugetConfigPath = "NuGet.Config";
//            var config = @"
//<configuration>
//    <Section>
//        <SubSection>
//            <add key='key0' value='value0' />
//            <add key='key1' value='value1' />
//            <add key='key2' value='value2' />
//        </SubSection>
//    </Section>
//</configuration>";
//            var expectedValues = new List<KeyValuePair<string, string>>()
//            {
//                new KeyValuePair<string, string>("key0","value0"),
//                new KeyValuePair<string, string>("key1","value1"),
//                new KeyValuePair<string, string>("key2","value2"),
//            };

//            using (var mockBaseDirectory = TestDirectory.Create())
//            {
//                ConfigurationFileTestUtility.CreateConfigurationFile(nugetConfigPath, mockBaseDirectory, config);
//                var settings = new SettingsFile(mockBaseDirectory);

//                // Act
//                var values = settings.GetNestedValues("Section", "SubSection");

//                // Assert
//                values.Should().NotBeNull();
//                values.Should().BeEquivalentTo(expectedValues);
//            }
//        }

//        [Fact]
//        public void CallingGetNestedValuesGetsMultipleValuesWithMetadataIgnoresDuplicates()
//        {
//            // Arrange
//            var nugetConfigPath = "NuGet.Config";
//            var config = @"
//<configuration>
//    <Section>
//        <SubSection>
//            <add key='key0' value='value0' />
//            <add key='key1' value='value1' meta1='data1' meta2='data2'/>
//            <add key='key2' value='value2' meta3='data3'/>
//        </SubSection>
//        <SubSection>
//            <add key='key3' value='value3' />
//        </SubSection>
//    </Section>
//</configuration>";
//            var expectedValues = new List<KeyValuePair<string, string>>()
//            {
//                new KeyValuePair<string, string>("key0","value0"),
//                new KeyValuePair<string, string>("key1","value1"),
//                new KeyValuePair<string, string>("key2","value2"),
//            };

//            using (var mockBaseDirectory = TestDirectory.Create())
//            {
//                ConfigurationFileTestUtility.CreateConfigurationFile(nugetConfigPath, mockBaseDirectory, config);
//                var settings = new SettingsFile(mockBaseDirectory);

//                // Act
//                var values = settings.GetNestedValues("Section", "SubSection");

//                // Assert
//                values.Should().NotBeNull();
//                values.Should().BeEquivalentTo(expectedValues);
//            }
//        }
    }
}
