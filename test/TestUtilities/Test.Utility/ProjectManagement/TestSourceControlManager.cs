// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using NuGet.ProjectManagement;

namespace Test.Utility
{
    public class TestSourceControlManager : SourceControlManager
    {
        public HashSet<string> PendAddedFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        public HashSet<string> PendDeletedFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        public TestSourceControlManager() : base(NuGet.Configuration.NullSettings.Instance) { }

        public TestSourceControlManager(NuGet.Configuration.ISettings settings) : base(settings) { }

        public override Stream CreateFile(string fullPath, INuGetProjectContext nuGetProjectContext)
        {
            PendAddedFiles.Add(fullPath);
            return FileSystemUtility.CreateFile(fullPath);
        }

        public override void PendAddFiles(
            IEnumerable<string> fullPaths,
            string root,
            INuGetProjectContext nuGetProjectContext)
        {
            foreach(var path in fullPaths)
            {
                PendAddedFiles.Add(path);
            }
        }

        public override void PendDeleteFiles(
            IEnumerable<string> fullPaths,
            string root,
            INuGetProjectContext nuGetProjectContext)
        {
            foreach(var path in fullPaths)
            {
                PendDeletedFiles.Add(path);
            }
        }
    }
}
