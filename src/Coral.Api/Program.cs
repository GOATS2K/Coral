using Coral.Api;
using Coral.Configuration;
using Coral.Database;
using Coral.Dto.Profiles;
using Coral.Encoders;
using Coral.Services;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.Extensions.FileProviders;
using Swashbuckle.AspNetCore.SwaggerGen;
using System.Reflection;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddDbContext<CoralDbContext>();
builder.Services.AddScoped<ILibraryService, LibraryService>();
builder.Services.AddScoped<IIndexerService, IndexerService>();
builder.Services.AddSingleton<IEncoderFactory, EncoderFactory>();
builder.Services.AddSingleton<ITranscodingJobManager, TranscodingJobManager>();
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

// setup file provider for hls
Directory.CreateDirectory(ApplicationConfiguration.HLSDirectory);
var fileProvider = new PhysicalFileProvider(ApplicationConfiguration.HLSDirectory);
var hlsRoute = "/hls";

// setup content type provider
var contentTypeProvider = new FileExtensionContentTypeProvider();
contentTypeProvider.Mappings[".m3u8"] = "application/x-MPEGurl";
contentTypeProvider.Mappings[".ts"] = "audio/MP2T";

app.UseStaticFiles(new StaticFileOptions()
{
    FileProvider = fileProvider,
    RequestPath = hlsRoute,
    ServeUnknownFileTypes = true,
    ContentTypeProvider = contentTypeProvider
});

app.UseAuthorization();

app.MapControllers();

using var scope = app.Services.CreateScope();
var db = scope.ServiceProvider.GetRequiredService<CoralDbContext>();
var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
await db.Database.EnsureCreatedAsync();

var indexerService = scope.ServiceProvider.GetRequiredService<IIndexerService>();
var contentDirectory = Environment.GetEnvironmentVariable("CORAL_CONTENT_DIRECTORY");
if (!string.IsNullOrEmpty(contentDirectory))
{
    indexerService.ReadDirectory(contentDirectory);
    app.Run();
}
logger.LogCritical("CORAL_CONTENT_DIRECTORY has not been set.");
