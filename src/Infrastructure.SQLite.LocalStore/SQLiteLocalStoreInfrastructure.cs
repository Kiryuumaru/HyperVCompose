using Application.LocalStore.Interfaces;
using ApplicationBuilderHelpers;
using Infrastructure.SQLite.LocalStore.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Infrastructure.SQLite.LocalStore;

public class SQLiteLocalStoreInfrastructure : SQLiteInfrastructure
{
    public override void AddServices(ApplicationDependencyBuilder builder, IServiceCollection services)
    {
        base.AddServices(builder, services);

        services.AddSingleton<SQLiteLocalStoreGlobalService>();
        services.AddScoped<ILocalStoreService, SQLiteLocalStoreService>();
    }
}
