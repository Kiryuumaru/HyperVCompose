using Application.Damper.Workers;
using Application.LocalStore.Services;
using ApplicationBuilderHelpers;
using Microsoft.Extensions.DependencyInjection;

namespace Application;

public class BaseApplication : ApplicationDependency
{
    public override void AddServices(ApplicationDependencyBuilder builder, IServiceCollection services)
    {
        base.AddServices(builder, services);

        services.AddSingleton<LocalStoreConcurrencyService>();
        services.AddTransient<LocalStoreService>();

        services.AddHostedService<DamperWorker>();
    }
}
