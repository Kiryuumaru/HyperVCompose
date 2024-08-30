﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Application.Common;

internal static class RoutineExecutor
{
    public static async void Execute(TimeSpan timingSpan, bool runFirst, Func<CancellationToken, Task> execute, Action<Exception> onError, CancellationToken stoppingToken)
    {
        DateTimeOffset hitTime = runFirst ? DateTimeOffset.MinValue : DateTimeOffset.UtcNow;

        while (!stoppingToken.IsCancellationRequested)
        {
            var now = DateTimeOffset.UtcNow;

            if (hitTime + timingSpan < now)
            {
                try
                {
                    hitTime = now;
                    await execute(stoppingToken);
                }
                catch (Exception ex)
                {
                    onError(ex);
                }
            }
            else
            {
                await Task.Delay(10);
            }
        }
    }

    public static void Execute(TimeSpan timingSpan, Func<CancellationToken, Task> execute, Action<Exception> onError, CancellationToken stoppingToken)
    {
        Execute(timingSpan, true, execute, onError, stoppingToken);
    }
}
