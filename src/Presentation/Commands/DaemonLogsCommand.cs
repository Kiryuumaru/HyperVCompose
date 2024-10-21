﻿using AbsolutePathHelpers;
using Application;
using ApplicationBuilderHelpers;
using CliFx;
using CliFx.Attributes;
using CliFx.Infrastructure;
using Infrastructure.Serilog;
using Infrastructure.SQLite.LocalStore;
using Microsoft.Extensions.Logging;
using Serilog;
using Application.Common;
using System.Runtime.InteropServices;
using Application.Logger.Interfaces;
using Microsoft.Extensions.Hosting;
using Serilog.Events;
using CliFx.Exceptions;

namespace Presentation.Commands;

[Command("daemon logs", Description = "Daemon logs command.")]
public class DaemonLogsCommand : MainCommand
{
    [CommandOption("tail", 't', Description = "Log lines print.")]
    public int Tail { get; set; } = 10;

    [CommandOption("follow", 'f', Description = "Follows logs.")]
    public bool Follow { get; set; }

    [CommandOption("scope", 's', Description = "Scope of logs.")]
    public IReadOnlyList<string>? Scope { get; set; }

    public override async ValueTask Run(ApplicationHostBuilder<WebApplicationBuilder> appBuilder, CancellationToken cancellationToken)
    {
        await base.Run(appBuilder, cancellationToken);

        var scopeMap = GetScopeMap();

        var appHost = appBuilder.Build();

        var loggerReader = appHost.Host.Services.GetRequiredService<ILoggerReader>();
        try
        {
            await loggerReader.Start(Tail, Follow, scopeMap, cancellationToken);
        }
        catch (OperationCanceledException) { }
    }

    public Dictionary<string, string> GetScopeMap()
    {
        Dictionary<string, string> scopePairs = [];
        if (Scope != null)
        {
            foreach (var s in Scope)
            {
                try
                {
                    var pair = s.Split('=');
                    if (pair.Length != 2)
                    {
                        throw new Exception();
                    }
                    scopePairs[pair[0]] = pair[1];
                }
                catch
                {
                    throw new CommandException($"Invalid scope value \"{s}\".", 1001);
                }
            }
        }
        return scopePairs;
    }
}
