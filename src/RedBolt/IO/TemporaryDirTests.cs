// Copyright (c) 2026 RedBolt and contributors.
namespace RedBolt.Tests.IO;

using System;
using System.IO;

using RedBolt.IO;

using File = global::System.IO.File;

public sealed class TemporaryDirTests
{
    [Fact]
    public void TemporaryDir_creates_and_removes_directory_tree()
    {
        var path = System.IO.Path.Combine(
            System.IO.Path.GetTempPath(), "RedBoltTemporaryDir-" + Guid.NewGuid().ToString("N"));

        try
        {
            using (var temporaryDir = new TemporaryDir(path))
            {
                Assert.True(Directory.Exists(path));
                Assert.EndsWith(System.IO.Path.DirectorySeparatorChar.ToString(), temporaryDir.Path);

                var childPath = System.IO.Path.Combine(path, "Child");
                Directory.CreateDirectory(childPath);
                File.WriteAllText(System.IO.Path.Combine(childPath, "file.txt"), "content");
            }

            Assert.False(Directory.Exists(path));
        }
        finally
        {
            if (Directory.Exists(path))
                Directory.Delete(path, true);
        }
    }
}
