using Coral.Database;
using Coral.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddDbContext<CoralDbContext>();
builder.Services.AddScoped<ILibraryService, LibraryService>();
builder.Services.AddScoped<IIndexerService, IndexerService>();
builder.Services.AddControllers();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

using var scope = app.Services.CreateScope();
var db = scope.ServiceProvider.GetRequiredService<CoralDbContext>();
await db.Database.EnsureCreatedAsync();

var indexerService = scope.ServiceProvider.GetRequiredService<IIndexerService>();
indexerService.ReadDirectory(@"P:\Coral Library");

app.Run();
