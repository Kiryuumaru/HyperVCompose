using Application.Damper.Workers;
using Application.LocalStore.Services;
using ApplicationBuilderHelpers;
using Microsoft.Extensions.DependencyInjection;

namespace Application;

public class Application : ApplicationDependency
{
    public override void AddServices(ApplicationHostBuilder applicationHostBuilder, IServiceCollection services)
    {
        base.AddServices(applicationHostBuilder, services);

        services.AddSingleton<LocalStoreConcurrencyService>();
        services.AddTransient<LocalStoreService>();

        services.AddHostedService<DamperWorker>();
    }
}
