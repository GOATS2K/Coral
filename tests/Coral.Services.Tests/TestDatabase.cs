using AutoMapper;
using Coral.Database;
using Coral.Dto.Profiles;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Coral.Services.Tests;

public class TestDatabase : IDisposable
{
    public CoralDbContext Context;
    public IMapper Mapper;
    private readonly IServiceProvider _serviceProvider;
    
    public TestDatabase()
    {
        var serviceCollection = new ServiceCollection();
        serviceCollection.AddDbContext<CoralDbContext>(options =>
        {
            options.UseSqlite("DataSource=:memory:");
        });
        serviceCollection.AddAutoMapper(opt =>
        {
            opt.AddMaps(typeof(TrackProfile));
        });
        _serviceProvider = serviceCollection.BuildServiceProvider();
        Context = _serviceProvider.GetRequiredService<CoralDbContext>();
        Mapper = _serviceProvider.GetRequiredService<IMapper>();
        Context.Database.OpenConnection();
        Context.Database.EnsureCreated();
    }

    public void Dispose()
    {
        Context.Database.CloseConnection();
        Context.Dispose();
    }
}