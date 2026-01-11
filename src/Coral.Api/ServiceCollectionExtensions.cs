using Coral.Database.Models;
using Coral.Encoders;
using Coral.Events;
using Coral.PluginBase;
using Coral.PluginHost;
using Coral.Services;
using Coral.Services.ChannelWrappers;
using Coral.Services.Helpers;
using Coral.Services.Indexer;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Infrastructure;

namespace Coral.Api
{
    public static class ServiceCollectionExtensions
    {
        public static void AddServices(this IServiceCollection services)
        {
            // Add memory caching for recommendations with size limit
            services.AddMemoryCache(options =>
            {
                options.SizeLimit = 1000; // Maximum 1000 cached entries
            });

            services.AddScoped<IArtworkMappingHelper, ArtworkMappingHelper>();
            services.AddScoped<IFavoritedMappingHelper, FavoritedMappingHelper>();
            services.AddScoped<ILibraryService, LibraryService>();
            services.AddScoped<IIndexerService, IndexerService>();
            services.AddScoped<ISearchService, SearchService>();
            services.AddScoped<IArtworkService, ArtworkService>();
            services.AddScoped<IPaginationService, PaginationService>();
            services.AddScoped<IPlaybackService, PlaybackService>();
            services.AddScoped<IPlaylistService, PlaylistService>();
            services.AddScoped<IFileSystemService, FileSystemService>();
            services.AddScoped<IFavoritesService, FavoritesService>();
            services.AddSingleton<IHostServiceProxy, HostServiceProxy>();
            services.AddSingleton<IPluginContext, PluginContext>();
            services.AddSingleton<TrackPlaybackEventEmitter>();
            services.AddSingleton<MusicLibraryRegisteredEventEmitter>();
            services.AddSingleton<IServiceProxy, ServiceProxy>();
            services.AddSingleton<IEncoderFactory, EncoderFactory>();
            services.AddSingleton<ITranscoderService, TranscoderService>();
            services.AddSingleton<IActionDescriptorChangeProvider>(MyActionDescriptorChangeProvider.Instance);
            services.AddSingleton(MyActionDescriptorChangeProvider.Instance);
            services.AddSingleton<IEmbeddingChannel, EmbeddingChannel>();
            services.AddSingleton<IEmbeddingService, EmbeddingService>();
            services.AddSingleton<InferenceService>();

            // Phase 1: New refactored services
            services.AddSingleton<IScanChannel, ScanChannel>();

            // Phase 2: Indexer refactored services
            services.AddScoped<IDirectoryScanner, DirectoryScanner>();
            services.AddSingleton<IScanReporter, ScanReporter>();

            // Auth services
            services.AddSingleton(TimeProvider.System);
            services.AddSingleton<ISessionCacheService, SessionCacheService>();
            services.AddScoped<IPasswordHasher<User>, PasswordHasher<User>>();
            services.AddScoped<IUserService, UserService>();
            services.AddScoped<IAuthService, AuthService>();
            services.AddScoped<IDeviceService, DeviceService>();
            services.AddSingleton<ISignedUrlService, SignedUrlService>();
        }
    }
}
