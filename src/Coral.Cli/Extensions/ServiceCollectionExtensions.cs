using Coral.Configuration;
using Coral.Database;
using Coral.Encoders;
using Coral.Events;
using Coral.Services;
using Coral.Services.ChannelWrappers;
using Coral.Services.Helpers;
using Coral.Services.Indexer;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Spectre.Console;

namespace Coral.Cli.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddCoralServices(this IServiceCollection services, IConfiguration configuration)
    {
        // Logging
        services.AddLogging(builder =>
        {
            builder.AddConsole();
            builder.AddConfiguration(configuration.GetSection("Logging"));
        });

        // Database
        services.AddDbContext<CoralDbContext>(ServiceLifetime.Scoped);

        // AutoMapper
        services.AddAutoMapper(opt =>
        {
            opt.AddMaps("Coral.Dto");
        });

        // Services from Coral.Api
        services.AddScoped<IArtworkMappingHelper, ArtworkMappingHelper>();
        services.AddScoped<ILibraryService, LibraryService>();
        services.AddScoped<IIndexerService, IndexerService>();
        services.AddScoped<ISearchService, SearchService>();
        services.AddScoped<IArtworkService, ArtworkService>();
        services.AddScoped<IPaginationService, PaginationService>();
        services.AddScoped<IPlaybackService, PlaybackService>();
        services.AddScoped<IFileSystemService, FileSystemService>();
        services.AddScoped<IFavoritesService, FavoritesService>();
        services.AddSingleton<TrackPlaybackEventEmitter>();
        services.AddSingleton<MusicLibraryRegisteredEventEmitter>();
        services.AddSingleton<IEncoderFactory, EncoderFactory>();
        services.AddSingleton<ITranscoderService, TranscoderService>();
        services.AddSingleton<IEmbeddingChannel, EmbeddingChannel>();
        services.AddSingleton<IEmbeddingService, EmbeddingService>();
        services.AddSingleton<InferenceService>();

        // Phase 1: New refactored services
        services.AddSingleton<IScanChannel, ScanChannel>();

        // Phase 2: Indexer refactored services
        services.AddScoped<IDirectoryScanner, DirectoryScanner>();

        // HttpClient for InferenceService
        services.AddHttpClient<InferenceService>();

        // Spectre Console
        services.AddSingleton<IAnsiConsole>(_ => AnsiConsole.Console);

        return services;
    }
}