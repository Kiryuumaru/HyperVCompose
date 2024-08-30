using Application.Common;
using Application.LocalStore.Services;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Application.Damper.Workers;

internal class DamperWorker(ILogger<DamperWorker> logger, IServiceProvider serviceProvider) : BackgroundService
{
    private readonly ILogger<DamperWorker> _logger = logger;
    private readonly IServiceProvider _serviceProvider = serviceProvider;

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        RoutineExecutor.Execute(TimeSpan.FromMilliseconds(500), false, stoppingToken, Routine, ex => _logger.LogError("Runner error: {msg}", ex.Message));
        return Task.CompletedTask;
    }

    private async Task Routine(CancellationToken stoppingToken)
    {
        var val = DateTimeOffset.UtcNow;

        _logger.LogTrace("I am debug {val}", val);
        await Task.Delay(100, stoppingToken);
        _logger.LogDebug("I am debug {val}", val);
        await Task.Delay(100, stoppingToken);
        _logger.LogInformation("I am info {val}", val);
        await Task.Delay(100, stoppingToken);
        _logger.LogWarning("I am warning {val}", val);
        await Task.Delay(100, stoppingToken);
        _logger.LogError("I am error {val}", val);
        await Task.Delay(100, stoppingToken);
        _logger.LogCritical("I am critical {val}", val);
        await Task.Delay(100, stoppingToken);
    }
}