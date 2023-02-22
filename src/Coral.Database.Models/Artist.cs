namespace Coral.Database.Models;

public class Artist : BaseTable
{
    public string Name { get; set; } = null!;

    public List<Album> Albums { get; set; } = null!;

}