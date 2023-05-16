using Coral.Encoders;
using Coral.Events;
using Coral.PluginBase;
using Coral.PluginHost;
using Coral.Services;
using Microsoft.AspNetCore.Mvc.Infrastructure;

namespace Coral.Api
{
    public static class ServiceCollectionExtensions
    {
        public static void AddServices(this IServiceCollection services)
        {
            services.AddScoped<ILibraryService, LibraryService>();
            services.AddScoped<IIndexerService, IndexerService>();
            services.AddScoped<ISearchService, SearchService>();
            services.AddScoped<IArtworkService, ArtworkService>();
            services.AddScoped<IPaginationService, PaginationService>();
            services.AddScoped<IPlaybackService, PlaybackService>();
            services.AddSingleton<IHostServiceProxy, HostServiceProxy>();
            services.AddSingleton<IPluginContext, PluginContext>();
            services.AddSingleton<TrackPlaybackEventEmitter>();
            services.AddSingleton<MusicLibraryRegisteredEventEmitter>();
            services.AddSingleton<IServiceProxy, ServiceProxy>();
            services.AddSingleton<IEncoderFactory, EncoderFactory>();
            services.AddSingleton<ITranscoderService, TranscoderService>();
            services.AddSingleton<IActionDescriptorChangeProvider>(MyActionDescriptorChangeProvider.Instance);
            services.AddSingleton(MyActionDescriptorChangeProvider.Instance);
        }
    }
}
