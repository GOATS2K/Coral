using Coral.Api;
using Coral.Api.Workers;
using Coral.Configuration;
using Coral.Configuration.Models;
using Coral.Database;
using Coral.Dto.Profiles;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
using Swashbuckle.AspNetCore.SwaggerGen;
using System.Reflection;

// Load Coral configuration
var coralConfig = ApplicationConfiguration.GetConfiguration();
ApplicationConfiguration.EnsureDirectoriesAreCreated();

var builder = WebApplication.CreateBuilder(args);

// Configure IOptions for ServerConfiguration
builder.Services.Configure<ServerConfiguration>(coralConfig);

// Add services to the container.
builder.Services.AddDbContext<CoralDbContext>();
builder.Services.AddServices();
builder.Services.AddHttpClient();
builder.Services.AddHostedService<PluginInitializer>();
builder.Services.AddHostedService<IndexerWorker>();
builder.Services.AddHostedService<EmbeddingWorker>();
builder.Services.AddHostedService<ScanWorker>(); // Phase 1: New scan worker
builder.Services.AddHttpContextAccessor();
builder.Services.AddAutoMapper(opt =>
{
    opt.AddMaps(typeof(TrackProfile));
});

builder.Services.AddControllers();
builder.Services.AddSignalR();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(conf =>
{
    conf.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo
    {
        Title = "Coral",
        Version = "v1",
    });
    conf.SupportNonNullableReferenceTypes();
    conf.SchemaFilter<RequiredNotNullableSchemaFilter>();
    // Use method name as operationId
    conf.CustomOperationIds(apiDesc => apiDesc.TryGetMethodInfo(out MethodInfo methodInfo) ? methodInfo.Name : null);
});
// return enums via their names instead of rank
builder.Services.Configure<Microsoft.AspNetCore.Http.Json.JsonOptions>(options =>
{
    options.SerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter());
});
builder.Services.Configure<Microsoft.AspNetCore.Mvc.JsonOptions>(options =>
{
    options.JsonSerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter());
}); 

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors(cors =>
{
    cors
    .SetIsOriginAllowed(_ => true)
    .AllowAnyMethod()
    .AllowAnyHeader()
    .AllowCredentials();
});

app.UseHttpsRedirection();

// setup content type provider
var contentTypeProvider = new FileExtensionContentTypeProvider();
contentTypeProvider.Mappings[".m3u8"] = "application/vnd.apple.mpegurl";
contentTypeProvider.Mappings[".m4s"] = "audio/mp4";

// serve HLS
app.UseStaticFiles(new StaticFileOptions()
{
    OnPrepareResponse = (ctx) =>
    {
        // HLS chunks should not be cached.
        ctx.Context.Response.Headers.Append("Cache-Control", "no-cache, no-store");
    },
    FileProvider = new PhysicalFileProvider(ApplicationConfiguration.HLSDirectory),
    RequestPath = "/hls",
    ServeUnknownFileTypes = true,
    ContentTypeProvider = contentTypeProvider,
});

// serve SPA route
app.UseStaticFiles();

app.UseAuthorization();

app.MapControllers();
app.MapHub<LibraryHub>("/hubs/library");
// could probably remap these to use query params instead in the frontend
app.MapFallbackToFile("/albums/{id}", "albums/[id].html");
app.MapFallbackToFile("/artists/{id}", "artists/[id].html");
app.MapFallbackToFile("/search/{id}", "search/search.html");
app.MapFallbackToFile("index.html");

try
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<CoralDbContext>();
    await db.Database.MigrateAsync();
}
catch (Exception ex)
{
    Console.WriteLine("Failed to run migrations.");
}
app.Run();
