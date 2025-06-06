using Nuke.Common.IO;
using NukeBuildHelpers;
using NukeBuildHelpers.Entry;
using NukeBuildHelpers.Entry.Extensions;
using NukeBuildHelpers.RunContext.Interfaces;
using NukeBuildHelpers.Runner.Abstraction;
using System.Collections.Generic;
using System;
using NukeBuildHelpers.RunContext.Extensions;
using Nuke.Common.Tools.DotNet;
using NukeBuildHelpers.Common;

class Build : BaseNukeBuildHelpers
{
    public static int Main() => Execute<Build>(x => x.Version);

    public override string[] EnvironmentBranches { get; } = ["prerelease", "master"];

    public override string MainEnvironmentBranch { get; } = "master";

    private static readonly string[] archMatrix = ["x64", "arm64"];

    BuildEntry BuildBinaries => _ => _
        .AppId("hyperv-compose")
        .Matrix(archMatrix, (definitionArch, arch) =>
        {
            definitionArch.RunnerOS(RunnerOS.Windows2022);
            definitionArch.WorkflowId($"build_windows_{arch}");
            definitionArch.DisplayName($"[Build] Windows{arch.ToUpperInvariant()}");
            definitionArch.Execute(context =>
            {
                string projectVersion = "0.0.0";
                if (context.TryGetVersionedContext(out var versionedContext))
                {
                    projectVersion = versionedContext.AppVersion.Version.WithoutMetadata().ToString();
                }
                var outAsset = GetOutAsset(arch);
                var archivePath = outAsset.Parent / outAsset.NameWithoutExtension;
                var outPath = archivePath / outAsset.NameWithoutExtension;
                var proj = RootDirectory / "src" / "Presentation" / "Presentation.csproj";
                DotNetTasks.DotNetBuild(_ => _
                    .SetProjectFile(proj)
                    .SetVersion(projectVersion)
                    .SetInformationalVersion(projectVersion)
                    .SetFileVersion(projectVersion)
                    .SetAssemblyVersion(projectVersion)
                    .SetConfiguration("Release"));
                DotNetTasks.DotNetPublish(_ => _
                    .SetProject(proj)
                    .SetConfiguration("Release")
                    .EnableSelfContained()
                    .SetRuntime($"win-{arch.ToLowerInvariant()}")
                    .SetVersion(projectVersion)
                    .SetInformationalVersion(projectVersion)
                    .SetFileVersion(projectVersion)
                    .SetAssemblyVersion(projectVersion)
                    .EnablePublishSingleFile()
                    .SetOutput(outPath));

                archivePath.ZipTo(outAsset);

                if (context.TryGetVersionedContext(out var versioned))
                {
                    (OutputDirectory / $"installer_{arch}.ps1").WriteAllText((RootDirectory / "installerTemplate.ps1").ReadAllText()
                        .Replace("{{$username}}", "Kiryuumaru")
                        .Replace("{{$repo}}", "HyperVCompose")
                        .Replace("{{$appname}}", $"HyperVCompose_Windows{arch.ToUpperInvariant()}")
                        .Replace("{{$appexec}}", "hvcc.exe")
                        .Replace("{{$rootextract}}", $"HyperVCompose_Windows{arch.ToUpperInvariant()}")
                        .Replace("{{$homepath}}", $"$env:ProgramData\\hvcc"));
                }
            });
        });

    PublishEntry PublishBinaries => _ => _
        .AppId("hyperv-compose")
        .RunnerOS(RunnerOS.Windows2022)
        .ReleaseAsset(() =>
        {
            List<AbsolutePath> paths = [];
            foreach (var arch in archMatrix)
            {
                paths.AddRange(GetAssets(arch));
            }
            return [.. paths];
        });

    string GetVersion(IRunContext context)
    {
        string version = "0.0.0";
        if (context.TryGetVersionedContext(out var versionedContext))
        {
            version = versionedContext.AppVersion.Version.ToString();
        }
        return version;
    }

    AbsolutePath GetOutAsset(string arch)
    {
        return OutputDirectory / ($"HyperVCompose_Windows{arch.ToUpperInvariant()}.zip");
    }

    AbsolutePath[] GetAssets(string arch)
    {
        List<AbsolutePath> assets = [];

        assets.Add(GetOutAsset(arch));

        assets.Add(OutputDirectory / $"installer_{arch}.ps1");

        return [.. assets];
    }
}
