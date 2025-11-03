using AutoMapper;
using Coral.Database;
using Coral.Database.Models;
using Coral.Dto.Profiles;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Coral.TestProviders;

public class TestDatabase
{
    public CoralDbContext Context;
    public IMapper Mapper;
    private IServiceProvider _serviceProvider;

    // a simple single with 2 tracks and a single artist
    public Artist Tatora;
    public Album BelieveBlankPagesSingle;
    public Track Believe;
    public Track BlankPages;
    public Genre DrumAndBass;

    // an extended EP with many guest features and a remix
    public Artist Lenzman;
    public ArtistWithRole LenzmanAsMain;
    public Artist Slay;
    public ArtistWithRole SlayAsGuest;
    public Artist DannySanchez;
    public ArtistWithRole DannySanchezAsGuest;
    public Artist Fox;
    public ArtistWithRole FoxAsGuest;
    public Artist Satl;
    public ArtistWithRole SatlAsGuest;
    public Artist Jubei;
    public ArtistWithRole JubeiAsRemixer;

    public Album ALittleWhileLonger;
    public Track LilSouljah;
    public Track Zusterliefde;
    public Track GimmeASec;
    public Track OldTimesSake;
    public Track Starlight;
    public Track Yasukuni;
    public Track Combo;
    public Track DownForWhatever;

    // a single track to test non-latin character search
    public Artist IchikoAoba;
    public ArtistWithRole IchikoAobaAsMain;
    public Artist RyuichiSakamoto;
    public ArtistWithRole RyuichiSakamotoAsGuest;
    public Track Fuwarin;
    public Genre Folk;
    public Album Radio;
    
    public TestDatabase(Action<DbContextOptionsBuilder> optionsAction)
    {
        var serviceCollection = new ServiceCollection();
        serviceCollection.AddDbContext<CoralDbContext>(optionsAction);
        serviceCollection.AddAutoMapper(opt => { opt.AddMaps(typeof(TrackProfile)); });
        _serviceProvider = serviceCollection.BuildServiceProvider();
        Context = _serviceProvider.GetRequiredService<CoralDbContext>();
        Mapper = _serviceProvider.GetRequiredService<IMapper>();
        Context.Database.EnsureCreated();

        var currentTime = DateTime.UtcNow;

        Context.Artists.Add(Tatora = new Artist()
        {
            Name = "Tatora",
            CreatedAt = currentTime,
        });

        Context.Artists.Add(Lenzman = new Artist()
        {
            Name = "Lenzman",
            CreatedAt = currentTime,
        });

        Context.Artists.Add(IchikoAoba = new Artist()
        {
            Name = "Ichiko Aoba",
            CreatedAt = currentTime,
        });

        Context.Artists.Add(RyuichiSakamoto = new Artist()
        {
            Name = "Ryuichi Sakamoto",
            CreatedAt = currentTime,
        });

        Context.ArtistsWithRoles.Add(LenzmanAsMain = new ArtistWithRole()
        {
            Role = ArtistRole.Main,
            Artist = Lenzman
        });

        Context.ArtistsWithRoles.Add(IchikoAobaAsMain = new ArtistWithRole()
        {
            Role = ArtistRole.Main,
            Artist = IchikoAoba
        });

        Context.ArtistsWithRoles.Add(RyuichiSakamotoAsGuest = new ArtistWithRole()
        {
            Role = ArtistRole.Guest,
            Artist = RyuichiSakamoto
        });


        Context.Artists.Add(Slay = new Artist()
        {
            Name = "Slay",
            CreatedAt = currentTime
        });

        Context.ArtistsWithRoles.Add(SlayAsGuest = new ArtistWithRole()
        {
            Role = ArtistRole.Guest,
            Artist = Slay
        });

        Context.Artists.Add(DannySanchez = new Artist()
        {
            Name = "Danny Sanchez",
            CreatedAt = currentTime,
        });

        Context.ArtistsWithRoles.Add(DannySanchezAsGuest = new ArtistWithRole()
        {
            Role = ArtistRole.Guest,
            Artist = DannySanchez
        });

        Context.Artists.Add(Fox = new Artist()
        {
            Name = "Fox",
            CreatedAt = currentTime
        });

        Context.ArtistsWithRoles.Add(FoxAsGuest = new ArtistWithRole()
        {
            Role = ArtistRole.Guest,
            Artist = Fox
        });

        Context.Artists.Add(Satl = new Artist()
        {
            Name = "Satl",
            CreatedAt = currentTime
        });

        Context.ArtistsWithRoles.Add(SatlAsGuest = new ArtistWithRole()
        {
            Role = ArtistRole.Guest,
            Artist = Satl,
        });

        Context.Artists.Add(Jubei = new Artist()
        {
            Name = "Jubei",
            CreatedAt = currentTime
        });

        Context.ArtistsWithRoles.Add(JubeiAsRemixer = new ArtistWithRole()
        {
            Role = ArtistRole.Remixer,
            Artist = Jubei
        });

        Context.Genres.Add(DrumAndBass = new Genre()
        {
            Name = "Drum & Bass",
            CreatedAt = currentTime,
        });

        Context.Genres.Add(Folk = new Genre()
        {
            Name = "Folk",
            CreatedAt = currentTime,
        });

        Context.Albums.Add(BelieveBlankPagesSingle = new Album()
        {
            Artists = new List<ArtistWithRole>(),
            CreatedAt = currentTime,
            Name = "Believe / Blank Pages",
            ReleaseYear = 2020,
            TrackTotal = 2,
            DiscTotal = 1,
        });

        Context.Albums.Add(ALittleWhileLonger = new Album()
        {
            Artists = new List<ArtistWithRole>()
            {
                LenzmanAsMain, SlayAsGuest, DannySanchezAsGuest, FoxAsGuest, SatlAsGuest, JubeiAsRemixer
            },
            CreatedAt = currentTime,
            Name = "A Little While Longer",
            Type = AlbumType.MiniAlbum,
            ReleaseYear = 2021,
            TrackTotal = 8,
            DiscTotal = 1,
            CoverFilePath = "nice album art"
        });

        Context.Albums.Add(Radio = new Album()
        {
            Artists = new List<ArtistWithRole>()
            {
                IchikoAobaAsMain, RyuichiSakamotoAsGuest
            },
            CreatedAt = currentTime,
            Name = "Radio",
            Type = AlbumType.Album,
            ReleaseYear = 2013,
            TrackTotal = 10,
            DiscTotal = 1,
            CoverFilePath = "nice album art"
        });

        Context.Tracks.Add(Believe = new Track()
        {
            Artists = new List<ArtistWithRole>()
            {
                new ArtistWithRole()
                {
                    Artist = Tatora,
                    Role = ArtistRole.Main
                }
            },
            Album = BelieveBlankPagesSingle,
            CreatedAt = currentTime,
            DurationInSeconds = 269,
            DiscNumber = 1,
            TrackNumber = 1,
            Title = "Believe",
            Genre = DrumAndBass,
            AudioFile = new AudioFile()
            {
                FilePath = "",
                FileSizeInBytes = 0,
                AudioMetadata = new AudioMetadata()
                {
                    Codec = "FLAC",
                    Channels = 2,
                    Bitrate = 1411,
                    SampleRate = 44100
                },
                Library = new MusicLibrary()
                {
                    LibraryPath = ""
                },
            },
            Keywords = new List<Keyword>(),
        });

        Context.Tracks.Add(BlankPages = new Track()
        {
            Artists = new List<ArtistWithRole>()
            {
                new ArtistWithRole()
                {
                    Artist = Tatora,
                    Role = ArtistRole.Main
                }
            },
            Album = BelieveBlankPagesSingle,
            CreatedAt = currentTime,
            DurationInSeconds = 243,
            DiscNumber = 1,
            TrackNumber = 1,
            Title = "Blank Pages",
            Genre = DrumAndBass,
            AudioFile = new AudioFile()
            {
                FilePath = "",
                FileSizeInBytes = 0,
                AudioMetadata = new AudioMetadata()
                {
                    Codec = "FLAC",
                    Channels = 2,
                    Bitrate = 1411,
                    SampleRate = 44100
                },
                Library = new MusicLibrary()
                {
                    LibraryPath = ""
                },
            },
            Keywords = new List<Keyword>(),
        });

        Context.Tracks.Add(LilSouljah = new Track()
        {
            Artists = new List<ArtistWithRole>()
            {
                LenzmanAsMain,
                SlayAsGuest
            },
            Album = ALittleWhileLonger,
            CreatedAt = currentTime,
            DurationInSeconds = 254,
            DiscNumber = 1,
            TrackNumber = 1,
            Title = "Lil Souljah (feat. Slay)",
            Genre = DrumAndBass,
            AudioFile = new AudioFile()
            {
                FilePath = "",
                FileSizeInBytes = 0,
                AudioMetadata = new AudioMetadata()
                {
                    Codec = "FLAC",
                    Channels = 2,
                    Bitrate = 1411,
                    SampleRate = 44100
                },
                Library = new MusicLibrary()
                {
                    LibraryPath = ""
                },
            },
            Keywords = new List<Keyword>(),
        });
        Context.Tracks.Add(Zusterliefde = new Track()
        {
            Artists = new List<ArtistWithRole>()
            {
                LenzmanAsMain
            },
            Album = ALittleWhileLonger,
            CreatedAt = currentTime,
            DurationInSeconds = 303,
            DiscNumber = 1,
            TrackNumber = 2,
            Title = "Zusterliefde",
            Genre = DrumAndBass,
            AudioFile = new AudioFile()
            {
                FilePath = "",
                FileSizeInBytes = 0,
                AudioMetadata = new AudioMetadata()
                {
                    Codec = "FLAC",
                    Channels = 2,
                    Bitrate = 1411,
                    SampleRate = 44100
                },
                Library = new MusicLibrary()
                {
                    LibraryPath = ""
                },
            },
            Keywords = new List<Keyword>(),
        });
        Context.Tracks.Add(GimmeASec = new Track()
        {
            Artists = new List<ArtistWithRole>()
            {
                LenzmanAsMain,
                DannySanchezAsGuest
            },
            Album = ALittleWhileLonger,
            CreatedAt = currentTime,
            DurationInSeconds = 272,
            DiscNumber = 1,
            TrackNumber = 3,
            Title = "Gimme a Sec (feat. Danny Sanchez)",
            Genre = DrumAndBass,
            AudioFile = new AudioFile()
            {
                FilePath = "",
                FileSizeInBytes = 0,
                AudioMetadata = new AudioMetadata()
                {
                    Codec = "FLAC",
                    Channels = 2,
                    Bitrate = 1411,
                    SampleRate = 44100
                },
                Library = new MusicLibrary()
                {
                    LibraryPath = ""
                },
            },
            Keywords = new List<Keyword>(),
        });
        Context.Tracks.Add(OldTimesSake = new Track()
        {
            Artists = new List<ArtistWithRole>()
            {
                LenzmanAsMain
            },
            Album = ALittleWhileLonger,
            CreatedAt = currentTime,
            DurationInSeconds = 319,
            DiscNumber = 1,
            TrackNumber = 4,
            Title = "Old Times' Sake",
            Genre = DrumAndBass,
            AudioFile = new AudioFile()
            {
                FilePath = "",
                FileSizeInBytes = 0,
                AudioMetadata = new AudioMetadata()
                {
                    Codec = "FLAC",
                    Channels = 2,
                    Bitrate = 1411,
                    SampleRate = 44100
                },
                Library = new MusicLibrary()
                {
                    LibraryPath = ""
                },
            },
            Keywords = new List<Keyword>(),
        });
        Context.Tracks.Add(Starlight = new Track()
        {
            Artists = new List<ArtistWithRole>()
            {
                LenzmanAsMain, FoxAsGuest
            },
            Album = ALittleWhileLonger,
            CreatedAt = currentTime,
            DurationInSeconds = 272,
            DiscNumber = 1,
            TrackNumber = 5,
            Title = "Starlight (feat. Fox)",
            Genre = DrumAndBass,
            AudioFile = new AudioFile()
            {
                FilePath = "",
                FileSizeInBytes = 0,
                AudioMetadata = new AudioMetadata()
                {
                    Codec = "FLAC",
                    Channels = 2,
                    Bitrate = 1411,
                    SampleRate = 44100
                },
                Library = new MusicLibrary()
                {
                    LibraryPath = ""
                },
            },
            Keywords = new List<Keyword>(),
        });
        Context.Tracks.Add(Yasukuni = new Track()
        {
            Artists = new List<ArtistWithRole>()
            {
                LenzmanAsMain
            },
            Album = ALittleWhileLonger,
            CreatedAt = currentTime,
            DurationInSeconds = 307,
            DiscNumber = 1,
            TrackNumber = 6,
            Title = "Yasukuni",
            Genre = DrumAndBass,
            AudioFile = new AudioFile()
            {
                FilePath = "",
                FileSizeInBytes = 0,
                AudioMetadata = new AudioMetadata()
                {
                    Codec = "FLAC",
                    Channels = 2,
                    Bitrate = 1411,
                    SampleRate = 44100
                },
                Library = new MusicLibrary()
                {
                    LibraryPath = ""
                },
            },
            Keywords = new List<Keyword>(),
        });
        Context.Tracks.Add(Combo = new Track()
        {
            Artists = new List<ArtistWithRole>()
            {
                LenzmanAsMain, SatlAsGuest
            },
            Album = ALittleWhileLonger,
            CreatedAt = currentTime,
            DurationInSeconds = 303,
            DiscNumber = 1,
            TrackNumber = 7,
            Title = "Combo (feat. Satl)",
            Genre = DrumAndBass,
            AudioFile = new AudioFile()
            {
                FilePath = "",
                FileSizeInBytes = 0,
                AudioMetadata = new AudioMetadata()
                {
                    Codec = "FLAC",
                    Channels = 2,
                    Bitrate = 1411,
                    SampleRate = 44100
                },
                Library = new MusicLibrary()
                {
                    LibraryPath = ""
                },
            },
            Keywords = new List<Keyword>(),
        });
        Context.Tracks.Add(DownForWhatever = new Track()
        {
            Artists = new List<ArtistWithRole>()
            {
                LenzmanAsMain, JubeiAsRemixer
            },
            Album = ALittleWhileLonger,
            CreatedAt = currentTime,
            DurationInSeconds = 330,
            DiscNumber = 1,
            TrackNumber = 8,
            Title = "Down For Whatever (Jubei Remix)",
            Genre = DrumAndBass,
            AudioFile = new AudioFile()
            {
                FilePath = "",
                FileSizeInBytes = 0,
                AudioMetadata = new AudioMetadata()
                {
                    Codec = "FLAC",
                    Channels = 2,
                    Bitrate = 1411,
                    SampleRate = 44100
                },
                Library = new MusicLibrary()
                {
                    LibraryPath = ""
                },
            },
            Keywords = new List<Keyword>(),
        });

        Context.Tracks.Add(Fuwarin = new Track()
        {
            Artists = new List<ArtistWithRole>()
            {
                IchikoAobaAsMain, RyuichiSakamotoAsGuest
            },
            Album = Radio,
            CreatedAt = currentTime,
            DurationInSeconds = 264,
            DiscNumber = 1,
            TrackNumber = 5,
            Title = "不和リン",
            AudioFile = new AudioFile()
            {
                FilePath = @"/tmp/coral/Ichiko Aoba (青葉市子) - Radio (ラヂヲ) (2013) [FLAC]/05 - Ichiko Aoba - 不和リン.flac",
                FileSizeInBytes = 0,
                AudioMetadata = new AudioMetadata()
                {
                    Codec = "FLAC",
                    Channels = 2,
                    Bitrate = 1411,
                    SampleRate = 44100
                },
                Library = new MusicLibrary()
                {
                    LibraryPath = ""
                },
            },
            Genre = Folk,
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