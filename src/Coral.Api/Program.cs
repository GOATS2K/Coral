using System.IdentityModel.Tokens.Jwt;
using System.Reflection;
using Coral.Api;
using Coral.Api.Middleware;
using Coral.Dto.Auth;
using Coral.Api.Workers;
using Coral.Configuration;
using Coral.Configuration.Models;
using Coral.Database;
using Coral.Dto.Profiles;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
using Microsoft.IdentityModel.Tokens;
using Swashbuckle.AspNetCore.SwaggerGen;

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
builder.Services.AddHostedService<EmbeddingWorker>();
builder.Services.AddHostedService<ScanWorker>();
builder.Services.AddHostedService<FileSystemWatcherWorker>();
builder.Services.AddHttpContextAccessor();
builder.Services.AddAutoMapper(opt =>
{
    opt.AddMaps(typeof(TrackProfile));
});

builder.Services.AddControllers();
builder.Services.AddSignalR();

// Authentication with Cookie + JWT Bearer schemes
var jwtSettings = coralConfig.GetSection("Jwt").Get<JwtSettings>() ?? new JwtSettings();
var jwtKey = new SymmetricSecurityKey(Convert.FromBase64String(jwtSettings.Secret));

builder.Services.AddAuthentication(options =>
{
    // Default scheme selection based on request
    options.DefaultScheme = AuthConstants.Schemes.CoralAuth;
    options.DefaultChallengeScheme = AuthConstants.Schemes.CoralAuth;
})
.AddCookie(CookieAuthenticationDefaults.AuthenticationScheme, options =>
{
    options.Cookie.Name = AuthConstants.Cookies.AuthCookie;
    options.Cookie.HttpOnly = true;
    options.Cookie.SecurePolicy = builder.Environment.EnvironmentName == "Production" ? CookieSecurePolicy.Always : CookieSecurePolicy.SameAsRequest;
    options.Cookie.SameSite = builder.Environment.EnvironmentName == "Production" ? SameSiteMode.Strict : SameSiteMode.Unspecified;
    options.ExpireTimeSpan = TimeSpan.FromDays(jwtSettings.SessionExpirationDays);
    options.SlidingExpiration = true;
    options.Events.OnRedirectToLogin = context =>
    {
        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
        return Task.CompletedTask;
    };
})
.AddJwtBearer(JwtBearerDefaults.AuthenticationScheme, options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = jwtKey,
        ValidateIssuer = false,
        ValidateAudience = false,
        ValidateLifetime = true,
        RequireExpirationTime = false, // Session validity is controlled by the database
        NameClaimType = JwtRegisteredClaimNames.Sub,
        RoleClaimType = AuthConstants.ClaimTypes.Role
    };
})
.AddPolicyScheme(AuthConstants.Schemes.CoralAuth, "Cookie or JWT", options =>
{
    options.ForwardDefaultSelector = context =>
    {
        // Check for Authorization header with Bearer token
        var authHeader = context.Request.Headers.Authorization.FirstOrDefault();
        if (!string.IsNullOrEmpty(authHeader) && authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            return JwtBearerDefaults.AuthenticationScheme;
        }

        // Check for cookie
        return context.Request.Cookies.ContainsKey(AuthConstants.Cookies.AuthCookie) 
            ? CookieAuthenticationDefaults.AuthenticationScheme
            // Default to JWT (will fail auth if no token, which is correct behavior)
            : JwtBearerDefaults.AuthenticationScheme;
    };
});
builder.Services.AddAuthorization();
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

app.UseAuthentication();
app.UseSessionValidation();
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

    // Initialize DuckDB embeddings database
    var embeddingService = scope.ServiceProvider.GetRequiredService<Coral.Services.IEmbeddingService>();
    await embeddingService.InitializeAsync();
}
catch (Exception ex)
{
    Console.WriteLine("Failed to run migrations or initialize databases.");
    Console.WriteLine(ex.Message);
}
app.Run();
