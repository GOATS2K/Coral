﻿namespace Coral.Database.Models;

public class Track : BaseTable
{
    public int? TrackNumber { get; set; }
    public int? DiscNumber { get; set; }
    public int DurationInSeconds { get; set; }

    public string Title { get; set; } = null!;
    public Artist Artist { get; set; } = null!;
    public Album Album { get; set; } = null!;
    public Genre? Genre { get; set; }

    public string? Comment { get; set; }

    public string FilePath { get; set; } = null!;
    public List<Keyword> Keywords { get; set; } = null!;

    public override string ToString()
    {
        var releaseYear = Album.ReleaseYear != null ? $"({Album.ReleaseYear})" : "";
        return $"{Artist.Name} - {Title} - {Album.Name} {releaseYear}";
    }
}
