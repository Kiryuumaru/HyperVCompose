using AbsolutePathHelpers;
using Application;
using Application.Configuration.Extensions;
using Serilog;
using System.Runtime.InteropServices;
using Application.Common;

namespace Presentation.Services;

internal class ServiceManager(ILogger<ServiceManager> logger, IConfiguration configuration)
{
    private readonly ILogger<ServiceManager> _logger = logger;
    private readonly IConfiguration _configuration = configuration;

    public async Task PrepareServiceWrapper(CancellationToken cancellationToken)
    {
        using var _ = _logger.BeginScopeMap(new()
        {
            ["Service"] = nameof(ServiceManager),
            ["ServiceManagerAction"] = nameof(PrepareServiceWrapper)
        });

        var home = _configuration.GetHomePath();

        var winswExecPath = home / "winsw.exe";

        if (!File.Exists(winswExecPath))
        {
            _logger.LogDebug("Service wrapper not found. Downloading...");

            string folderName;
            if (RuntimeInformation.ProcessArchitecture == Architecture.X64)
            {
                folderName = "winsw_windows_x64";
            }
            else if (RuntimeInformation.ProcessArchitecture == Architecture.Arm64)
            {
                folderName = "winsw_windows_arm64";
            }
            else
            {
                throw new NotSupportedException();
            }
            string dlUrl = $"https://github.com/Kiryuumaru/winsw-modded/releases/download/build.1/{folderName}.zip";
            var downloadsPath = home / "downloads";
            var winswZipPath = downloadsPath / "winsw.zip";
            var winswZipExtractPath = downloadsPath / "winsw";
            var winswDownloadedExecPath = winswZipExtractPath / folderName / "winsw.exe";
            try
            {
                await winswZipPath.Delete(cancellationToken);
            }
            catch { }
            try
            {
                await winswZipExtractPath.Delete(cancellationToken);
            }
            catch { }
            downloadsPath.CreateDirectory();
            winswZipExtractPath.CreateDirectory();
            {
                using var client = new HttpClient();
                using var s = await client.GetStreamAsync(dlUrl, cancellationToken: cancellationToken);
                using var fs = new FileStream(winswZipPath, FileMode.OpenOrCreate);
                await s.CopyToAsync(fs, cancellationToken: cancellationToken);
            }
            await winswZipPath.UnZipTo(winswZipExtractPath, cancellationToken);
            await winswDownloadedExecPath.CopyTo(winswExecPath, cancellationToken);

            _logger.LogDebug("Service wrapper downloaded");
        }

        var config = $"""
            <service>
              <id>{Defaults.AppNameKebabCase}</id>
              <name>Hyper-V Composer</name>
              <description>Hyper-V Composer API Service for managing Hyper-V VM instances</description>
              <executable>%BASE%\hvc.exe</executable>
              <arguments>daemon run</arguments>
              <log mode="none"></log>
              <startmode>Automatic</startmode>
              <onfailure action="restart" delay="2 sec"/>
              <env name="ASPNETCORE_URLS" value="http://*:23456" />
            </service>
            """;

        var serviceConfig = home / "svc.xml";
        await File.WriteAllTextAsync(serviceConfig, config, cancellationToken);
    }

    public async Task UpdateClient(CancellationToken cancellationToken)
    {
        using var _ = _logger.BeginScopeMap(new()
        {
            ["Service"] = nameof(ServiceManager),
            ["ServiceManagerAction"] = nameof(UpdateClient)
        });

        var home = _configuration.GetHomePath();

        var cliClientExec = home / "hvc.exe";

        _logger.LogDebug("Service wrapper not found. Downloading...");

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
        string dlUrl = $"https://github.com/Kiryuumaru/HyperVCompose/releases/latest/download/{folderName}.zip";
        var downloadsPath = home / "downloads";
        var winswZipPath = downloadsPath / "HyperVCompose.zip";
        var winswZipExtractPath = downloadsPath / "HyperVCompose";
        var winswDownloadedExecPath = winswZipExtractPath / folderName / "hvc.exe";
        try
        {
            await winswZipPath.Delete(cancellationToken);
        }
        catch { }
        try
        {
            await winswZipExtractPath.Delete(cancellationToken);
        }
        catch { }
        downloadsPath.CreateDirectory();
        winswZipExtractPath.CreateDirectory();
        {
            using var client = new HttpClient();
            using var s = await client.GetStreamAsync(dlUrl, cancellationToken: cancellationToken);
            using var fs = new FileStream(winswZipPath, FileMode.OpenOrCreate);
            await s.CopyToAsync(fs, cancellationToken: cancellationToken);
        }
        await winswZipPath.UnZipTo(winswZipExtractPath, cancellationToken);
        await cliClientExec.Delete(cancellationToken);
        File.CreateSymbolicLink(cliClientExec, winswDownloadedExecPath);

        _logger.LogDebug("Service wrapper downloaded");
    }

    public async Task Install(string? username, string? password, CancellationToken cancellationToken)
    {
        using var _ = _logger.BeginScopeMap(new()
        {
            ["Service"] = nameof(ServiceManager),
            ["ServiceManagerAction"] = nameof(Install)
        });

        _logger.LogInformation("Installing service...");

        var home = _configuration.GetHomePath();

        await PrepareServiceWrapper(cancellationToken);

        var winswExecPath = home / "winsw.exe";
        var serviceConfig = home / "svc.xml";

        try
        {
            await Cli.RunListenAndLog(_logger, $"{winswExecPath} stop {serviceConfig} --force", stoppingToken: cancellationToken);
            _logger.LogDebug("Existing service stopped");
            await Cli.RunListenAndLog(_logger, $"{winswExecPath} uninstall {serviceConfig}", stoppingToken: cancellationToken);
            _logger.LogDebug("Existing service uninstalled");
        }
        catch { }
        if (!string.IsNullOrEmpty(username) || !string.IsNullOrEmpty(password))
        {
            if (string.IsNullOrEmpty(username) && string.IsNullOrEmpty(password))
            {
                throw new Exception("Both username and password must be specified");
            }

            await Cli.RunListenAndLog(_logger, $"{winswExecPath} install {serviceConfig} --username \"{username}\" --password \"{password}\"", stoppingToken: cancellationToken);
        }
        else
        {
            await Cli.RunListenAndLog(_logger, $"{winswExecPath} install {serviceConfig}", stoppingToken: cancellationToken);
        }

        _logger.LogInformation("Service installed");
    }

    public async Task Start(CancellationToken cancellationToken)
    {
        using var _ = _logger.BeginScopeMap(new()
        {
            ["Service"] = nameof(ServiceManager),
            ["ServiceManagerAction"] = nameof(Start)
        });

        _logger.LogInformation("Starting service...");

        var home = _configuration.GetHomePath();

        await PrepareServiceWrapper(cancellationToken);

        var winswExecPath = home / "winsw.exe";
        var serviceConfig = home / "svc.xml";

        try
        {
            await Cli.RunListenAndLog(_logger, $"{winswExecPath} stop {serviceConfig} --force", stoppingToken: cancellationToken);
            _logger.LogDebug("Existing service stopped");
        }
        catch { }
        await Cli.RunListenAndLog(_logger, $"{winswExecPath} start {serviceConfig}", stoppingToken: cancellationToken);

        _logger.LogInformation("Service started");
    }

    public async Task Stop(CancellationToken cancellationToken)
    {
        using var _ = _logger.BeginScopeMap(new()
        {
            ["Service"] = nameof(ServiceManager),
            ["ServiceManagerAction"] = nameof(Stop)
        });

        _logger.LogInformation("Stopping service...");

        var home = _configuration.GetHomePath();

        await PrepareServiceWrapper(cancellationToken);

        var winswExecPath = home / "winsw.exe";
        var serviceConfig = home / "svc.xml";

        await Cli.RunListenAndLog(_logger, $"{winswExecPath} stop {serviceConfig} --force", stoppingToken: cancellationToken);

        _logger.LogInformation("Service stopped");
    }

    public async Task Uninstall(CancellationToken cancellationToken)
    {
        using var _ = _logger.BeginScopeMap(new()
        {
            ["Service"] = nameof(ServiceManager),
            ["ServiceManagerAction"] = nameof(Uninstall)
        });

        _logger.LogInformation("Uninstalling service...");

        var home = _configuration.GetHomePath();

        await PrepareServiceWrapper(cancellationToken);

        var winswExecPath = home / "winsw.exe";
        var serviceConfig = home / "svc.xml";

        try
        {
            await Cli.RunListenAndLog(_logger, $"{winswExecPath} stop {serviceConfig} --force", stoppingToken: cancellationToken);
            _logger.LogDebug("Existing service stopped");
        }
        catch { }
        await Cli.RunListenAndLog(_logger, $"{winswExecPath} uninstall {serviceConfig}", stoppingToken: cancellationToken);

        _logger.LogInformation("Service uninstalled");
    }
}
