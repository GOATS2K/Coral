using Coral.Api;
using Coral.Configuration;
using Coral.Database;
using Coral.Dto.Profiles;
using Coral.Encoders;
using Coral.PluginHost;
using Coral.Services;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
using Swashbuckle.AspNetCore.SwaggerGen;
using System.Reflection;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddDbContext<CoralDbContext>();
builder.Services.AddScoped<ILibraryService, LibraryService>();
builder.Services.AddScoped<IIndexerService, IndexerService>();
builder.Services.AddScoped<ISearchService, SearchService>();
builder.Services.AddScoped<IArtworkService, ArtworkService>();
builder.Services.AddScoped<IPaginationService, PaginationService>();
builder.Services.AddSingleton<IPluginContext, PluginContext>();
builder.Services.AddSingleton<IServiceProxy, ServiceProxy>();
builder.Services.AddSingleton<IEncoderFactory, EncoderFactory>();
builder.Services.AddSingleton<ITranscoderService, TranscoderService>();
builder.Services.AddSingleton<IActionDescriptorChangeProvider>(MyActionDescriptorChangeProvider.Instance);
builder.Services.AddSingleton(MyActionDescriptorChangeProvider.Instance);
builder.Services.AddHostedService<PluginInitializer>();
builder.Services.AddHttpContextAccessor();
builder.Services.AddAutoMapper(opt =>
{
    opt.AddMaps(typeof(TrackProfile));
});

builder.Services.AddControllers();
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
    conf.CustomOperationIds(apiDesc =>
    {
        return apiDesc.TryGetMethodInfo(out MethodInfo methodInfo) ? methodInfo.Name : null;
    });
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
    cors.
    AllowAnyOrigin()
    .AllowAnyMethod()
    .AllowAnyHeader();
});

app.UseHttpsRedirection();

// setup content type provider
var contentTypeProvider = new FileExtensionContentTypeProvider();
contentTypeProvider.Mappings[".m3u8"] = "application/vnd.apple.mpegurl";
contentTypeProvider.Mappings[".ts"] = "audio/mp2t";
contentTypeProvider.Mappings[".m4s"] = "audio/mp4";

Directory.CreateDirectory(ApplicationConfiguration.HLSDirectory);
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
// could probably remap these to use query params instead in the frontend
app.MapFallbackToFile("/albums/{id}", "albums/[id].html");
app.MapFallbackToFile("index.html");

using var scope = app.Services.CreateScope();
var db = scope.ServiceProvider.GetRequiredService<CoralDbContext>();
await db.Database.MigrateAsync();
app.Run();
