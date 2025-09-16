using Coral.Encoders;
using Coral.Essentia.Bindings;
using Coral.Events;
using Coral.PluginBase;
using Coral.PluginHost;
using Coral.Services;
using Coral.Services.ChannelWrappers;
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
            services.AddScoped<IFileSystemService, FileSystemService>();
            services.AddSingleton<IHostServiceProxy, HostServiceProxy>();
            services.AddSingleton<IPluginContext, PluginContext>();
            services.AddSingleton<TrackPlaybackEventEmitter>();
            services.AddSingleton<MusicLibraryRegisteredEventEmitter>();
            services.AddSingleton<IServiceProxy, ServiceProxy>();
            services.AddSingleton<IEncoderFactory, EncoderFactory>();
            services.AddSingleton<ITranscoderService, TranscoderService>();
            services.AddSingleton<IActionDescriptorChangeProvider>(MyActionDescriptorChangeProvider.Instance);
            services.AddSingleton(MyActionDescriptorChangeProvider.Instance);
            services.AddSingleton<EssentiaService>(_ =>
            {
                // TODO: Get this from config
                var modelPath = @"C:\Users\bootie-\Downloads\discogs_track_embeddings-effnet-bs64-1.pb";
                var e = new EssentiaService();
                e.LoadModel(modelPath);
                return e;
            });
            services.AddSingleton<IEmbeddingChannel, EmbeddingChannel>();
        }
    }
}
