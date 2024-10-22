using AbsolutePathHelpers;
using Application;
using Application.Configuration.Extensions;
using Serilog;
using System.Runtime.InteropServices;
using Application.Common;

namespace Presentation.Services;

internal class ClientManager(ILogger<ClientManager> logger, IConfiguration configuration, ServiceManager serviceManager, DaemonManager daemonManager)
{
    private readonly ILogger<ClientManager> _logger = logger;
    private readonly IConfiguration _configuration = configuration;
    private readonly ServiceManager _serviceManager = serviceManager;
    private readonly DaemonManager _daemonManager = daemonManager;

    public async Task UpdateClient(CancellationToken cancellationToken)
    {
        using var _ = _logger.BeginScopeMap(new()
        {
            ["Service"] = nameof(ClientManager),
            ["ClientManagerAction"] = nameof(UpdateClient)
        });

        _logger.LogInformation("Downloading latest client...");

        string folderName;
        if (RuntimeInformation.ProcessArchitecture == Architecture.X64)
        {
            folderName = "HyperVCompose_WindowsX64";
        }
        else if (RuntimeInformation.ProcessArchitecture == Architecture.Arm64)
        {
            folderName = "HyperVCompose_WindowsARM64";
        }
        else
        {
            throw new NotSupportedException();
        }

        await _serviceManager.Download(
            Defaults.AppNameKebabCase,
            $"https://github.com/Kiryuumaru/HyperVCompose/releases/latest/download/{folderName}.zip",
            "latest",
            async extractFactory =>
            {
                var extractTemp = _configuration.GetTempPath() / $"hvc-{Guid.NewGuid()}";
                await extractFactory.DownloadedFilePath.UnZipTo(extractTemp, cancellationToken);
                await (extractTemp / folderName / "hvc.exe").CopyTo(extractFactory.ExtractDirectory / "hvc.exe");
            },
            executableLinkFactory => [(executableLinkFactory / "hvc.exe", "hvc.exe")],
            cancellationToken);

        _logger.LogInformation("Latest client downloaded");
    }

    public async Task Install(string? username, string? password, CancellationToken cancellationToken)
    {
        using var _ = _logger.BeginScopeMap(new()
        {
            ["Service"] = nameof(ClientManager),
            ["ClientManagerAction"] = nameof(Install)
        });

        _logger.LogInformation("Installing client...");

        var hvcServicePath = await _serviceManager.GetCurrentServicePath(Defaults.AppNameKebabCase, cancellationToken) ?? throw new Exception("HVC client was not downloaded");
        var hvcExecPath = hvcServicePath / "hvc.exe";

        await _daemonManager.Install(
            Defaults.AppNameKebabCase,
            Defaults.AppNameReadable,
            Defaults.AppNameDescription,
            hvcExecPath,
            "daemon run",
            username,
            password,
            new Dictionary<string, string>
            {
                ["ASPNETCORE_URLS"] = "http://*:23456"
            },
            cancellationToken);

        _logger.LogInformation("Service client installed");
    }

    public async Task Start(CancellationToken cancellationToken)
    {
        using var _ = _logger.BeginScopeMap(new()
        {
            ["Service"] = nameof(ClientManager),
            ["ClientManagerAction"] = nameof(Start)
        });

        _logger.LogInformation("Starting client service...");

        await _daemonManager.Start(Defaults.AppNameKebabCase, cancellationToken);

        _logger.LogInformation("Service client started");
    }

    public async Task Stop(CancellationToken cancellationToken)
    {
        using var _ = _logger.BeginScopeMap(new()
        {
            ["Service"] = nameof(ClientManager),
            ["ClientManagerAction"] = nameof(Stop)
        });

        _logger.LogInformation("Stopping client service...");

        await _daemonManager.Stop(Defaults.AppNameKebabCase, cancellationToken);

        _logger.LogInformation("Service client stopped");
    }

    public async Task Uninstall(CancellationToken cancellationToken)
    {
        using var _ = _logger.BeginScopeMap(new()
        {
            ["Service"] = nameof(ClientManager),
            ["ClientManagerAction"] = nameof(Uninstall)
        });

        _logger.LogInformation("Uninstalling client service...");

        await _daemonManager.Uninstall(Defaults.AppNameKebabCase, cancellationToken);

        _logger.LogInformation("Service client uninstalled");
    }
}
