using Coral.Database;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Coral.Services.Tests;

public class TestDatabase : IDisposable
{
    public CoralDbContext Context;
    private readonly IServiceProvider _serviceProvider;
    
    public TestDatabase()
    {
        var serviceCollection = new ServiceCollection();
        serviceCollection.AddDbContext<CoralDbContext>(options =>
        {
            options.UseSqlite("DataSource=:memory:");
        });
        _serviceProvider = serviceCollection.BuildServiceProvider();

        Context = _serviceProvider.GetRequiredService<CoralDbContext>();
        Context.Database.OpenConnection();
        Context.Database.EnsureCreated();
    }

    public void Dispose()
    {
        Context.Database.CloseConnection();
        Context.Dispose();
    }
}