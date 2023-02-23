using AutoMapper;
using Coral.Database;
using Coral.Database.Models;
using Coral.Dto.Profiles;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Coral.TestProviders;

public class TestDatabase : IDisposable
{
    public CoralDbContext Context;
    public IMapper Mapper;
    private readonly IServiceProvider _serviceProvider;

    public Artist Tatora;
    public Album BelieveBlankPagesSingle;
    public Track Believe;
    public Track BlankPages;
    public Genre DrumAndBass;

    public Artist Lenzman;
    public Album ALittleWhileLonger;
    public Track LilSouljah;
    public Track Zusterliefde;
    public Track GimmeASec;
    public Track OldTimesSake;
    public Track Starlight;
    public Track Yasukuni;
    public Track Combo;
    public Track DownForWhatever;



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

        var currentTime = DateTime.UtcNow;

        Context.Artists.Add(Tatora = new Artist()
        {
            Name = "Tatora",
            DateIndexed = currentTime,
            Albums = new List<Album>()
        });

        Context.Artists.Add(Lenzman = new Artist()
        {
            Name = "Lenzman",
            DateIndexed = currentTime,
            Albums = new List<Album>()
        });

        Context.Genres.Add(DrumAndBass = new Genre()
        {
            Name = "Drum & Bass",
            DateIndexed = currentTime,
        });

        Context.Albums.Add(BelieveBlankPagesSingle = new Album()
        {
            Artists = new List<Artist>() { Tatora },
            DateIndexed = currentTime,
            Name = "Believe / Blank Pages",
            ReleaseYear = 2020,
            TrackTotal = 2,
            DiscTotal = 1,
        });

        Context.Albums.Add(ALittleWhileLonger = new Album()
        {
            Artists = new List<Artist>() { Lenzman },
            DateIndexed = currentTime,
            Name = "A Little While Longer",
            ReleaseYear = 2021,
            TrackTotal = 8,
            DiscTotal = 1,
            CoverFilePath = "nice album art"
        });

        Context.Tracks.Add(Believe = new Track()
        {
            Artists = new List<ArtistOnTrack>()
            {
                new ArtistOnTrack()
                {
                    Artist = Tatora,
                    Role = ArtistRole.Main
                }
            },
            Album = BelieveBlankPagesSingle,
            DateIndexed = currentTime,
            DurationInSeconds = 269,
            DiscNumber = 1,
            TrackNumber = 1,
            Title = "Believe",
            FilePath = "",
            Genre = DrumAndBass,
            Keywords = new List<Keyword>(),
        });

        Context.Tracks.Add(BlankPages = new Track()
        {
            Artists = new List<ArtistOnTrack>()
            {
                new ArtistOnTrack()
                {
                    Artist = Tatora,
                    Role = ArtistRole.Main
                }
            },
            Album = BelieveBlankPagesSingle,
            DateIndexed = currentTime,
            DurationInSeconds = 243,
            DiscNumber = 1,
            TrackNumber = 1,
            Title = "Blank Pages",
            FilePath = "",
            Genre = DrumAndBass,
            Keywords = new List<Keyword>(),
        });

        Context.Tracks.Add(LilSouljah = new Track()
        {
            Artists = new List<ArtistOnTrack>()
            {
                new ArtistOnTrack()
                {
                    Artist = Lenzman,
                    Role = ArtistRole.Main
                },
                new ArtistOnTrack()
                {
                    Artist = new Artist(){
                        Name = "Slay"
                    },
                    Role = ArtistRole.Guest
                },

            },
            Album = ALittleWhileLonger,
            DateIndexed = currentTime,
            DurationInSeconds = 254,
            DiscNumber = 1,
            TrackNumber = 1,
            Title = "Lil Souljah (feat. Slay)",
            FilePath = "",
            Genre = DrumAndBass,
            Keywords = new List<Keyword>(),
        });
        Context.Tracks.Add(Zusterliefde = new Track()
        {
            Artists = new List<ArtistOnTrack>()
            {
                new ArtistOnTrack()
                {
                    Artist = Lenzman,
                    Role = ArtistRole.Main
                },
            },
            Album = ALittleWhileLonger,
            DateIndexed = currentTime,
            DurationInSeconds = 303,
            DiscNumber = 1,
            TrackNumber = 2,
            Title = "Zusterliefde",
            FilePath = "",
            Genre = DrumAndBass,
            Keywords = new List<Keyword>(),
        });
        Context.Tracks.Add(GimmeASec = new Track()
        {
            Artists = new List<ArtistOnTrack>()
            {
                new ArtistOnTrack()
                {
                    Artist = Lenzman,
                    Role = ArtistRole.Main
                },
                new ArtistOnTrack()
                {
                    Artist = new Artist(){
                        Name = "Danny Sanchez"
                    },
                    Role = ArtistRole.Guest
                },

            },
            Album = ALittleWhileLonger,
            DateIndexed = currentTime,
            DurationInSeconds = 272,
            DiscNumber = 1,
            TrackNumber = 3,
            Title = "Gimme a Sec (feat. Danny Sanchez)",
            FilePath = "",
            Genre = DrumAndBass,
            Keywords = new List<Keyword>(),
        });
        Context.Tracks.Add(OldTimesSake = new Track()
        {
            Artists = new List<ArtistOnTrack>()
            {
                new ArtistOnTrack() { Artist = Lenzman, Role = ArtistRole.Main }, 
            },
            Album = ALittleWhileLonger,
            DateIndexed = currentTime,
            DurationInSeconds = 319,
            DiscNumber = 1,
            TrackNumber = 4,
            Title = "Old Times' Sake",
            FilePath = "",
            Genre = DrumAndBass,
            Keywords = new List<Keyword>(),
        });
        Context.Tracks.Add(Starlight = new Track()
        {
            Artists = new List<ArtistOnTrack>()
            {
                new ArtistOnTrack() { Artist = Lenzman, Role = ArtistRole.Main },
                new ArtistOnTrack() { Artist = new Artist() { Name = "Fox" }, Role = ArtistRole.Guest },
            },
            Album = ALittleWhileLonger,
            DateIndexed = currentTime,
            DurationInSeconds = 272,
            DiscNumber = 1,
            TrackNumber = 5,
            Title = "Starlight (feat. Fox)",
            FilePath = "",
            Genre = DrumAndBass,
            Keywords = new List<Keyword>(),
        });
        Context.Tracks.Add(Yasukuni = new Track()
        {
            Artists = new List<ArtistOnTrack>()
            {
                new ArtistOnTrack()
                {
                    Artist = Lenzman,
                    Role = ArtistRole.Main
                },
            },
            Album = ALittleWhileLonger,
            DateIndexed = currentTime,
            DurationInSeconds = 307,
            DiscNumber = 1,
            TrackNumber = 6,
            Title = "Yasukuni",
            FilePath = "",
            Genre = DrumAndBass,
            Keywords = new List<Keyword>(),
        });
        Context.Tracks.Add(Combo = new Track()
        {
            Artists = new List<ArtistOnTrack>()
            {
                new ArtistOnTrack() { Artist = Lenzman, Role = ArtistRole.Main },
                new ArtistOnTrack() { Artist = new Artist() { Name = "Satl" }, Role = ArtistRole.Guest },
            },
            Album = ALittleWhileLonger,
            DateIndexed = currentTime,
            DurationInSeconds = 303,
            DiscNumber = 1,
            TrackNumber = 7,
            Title = "Combo (feat. Satl)",
            FilePath = "",
            Genre = DrumAndBass,
            Keywords = new List<Keyword>(),
        });
        Context.Tracks.Add(DownForWhatever = new Track()
        {
            Artists = new List<ArtistOnTrack>()
            {
                new ArtistOnTrack() { Artist = Lenzman, Role = ArtistRole.Main },
                new ArtistOnTrack() { Artist = new Artist() { Name = "Jubei" }, Role = ArtistRole.Remixer },
            },
            Album = ALittleWhileLonger,
            DateIndexed = currentTime,
            DurationInSeconds = 330,
            DiscNumber = 1,
            TrackNumber = 8,
            Title = "Down For Whatever (Jubei Remix)",
            FilePath = "",
            Genre = DrumAndBass,
            Keywords = new List<Keyword>(),
        });

        Context.SaveChanges();
    }

    public void Dispose()
    {
        Context.Database.CloseConnection();
        Context.Dispose();
    }
}