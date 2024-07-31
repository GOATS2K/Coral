namespace Coral.Database.Models;

public class RecordLabel : BaseTable
{
    public string Name { get; set; } = null!;
    public List<Album> Releases { get; set; } = null!;
}