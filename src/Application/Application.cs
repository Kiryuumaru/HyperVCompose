using Application.Damper.Workers;
using Application.LocalStore.Services;
using Application.ServiceMaster.Services;
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
        services.AddScoped<ServiceManagerService>();
        services.AddScoped<DaemonManagerService>();

        services.AddHostedService<DamperWorker>();
    }
}
