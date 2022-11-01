namespace Coral.Database.Models;

public class Artist
{
    public int Id { get; set; }
    public string Name { get; set; } = null!;

    public List<Album> Albums { get; set; } = null!;

    public DateTime DateIndexed { get; set; }

}