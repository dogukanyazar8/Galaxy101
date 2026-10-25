using System;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;

// The test host starts in the build output folder; fixtures and the v4 package load by repo-relative paths,
// which is how Unity runs them (its working directory is the project root).
[SetUpFixture]
public sealed class RepoRootSetUp
{
    [OneTimeSetUp]
    public void UseRepoRootAsWorkingDirectory()
    {
        var root = typeof(RepoRootSetUp).Assembly.GetCustomAttributes<AssemblyMetadataAttribute>()
            .FirstOrDefault(a => a.Key == "RepoRoot")?.Value;
        var fromEnvironment = Environment.GetEnvironmentVariable("OKEY101_REPO_ROOT");
        if (!string.IsNullOrEmpty(fromEnvironment)) root = fromEnvironment;
        if (!string.IsNullOrEmpty(root) && Directory.Exists(Path.Combine(root, "Assets")))
            Directory.SetCurrentDirectory(root);
    }
}
