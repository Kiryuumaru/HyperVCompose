using Application.LocalStore.Interfaces;
using ApplicationBuilderHelpers;
using Infrastructure.SQLite.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Infrastructure.SQLite;

public class SQLiteInfrastructure : ApplicationDependency
{
    public override void AddServices(ApplicationDependencyBuilder builder, IServiceCollection services)
    {
        base.AddServices(builder, services);

        services.AddSingleton<SQLiteGlobalService>();
    }
}
