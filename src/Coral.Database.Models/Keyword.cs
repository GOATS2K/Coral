namespace Coral.Database.Models
{
    public class Keyword : BaseTable
    {
        public string Value { get; set; } = null!;
        public List<Track> Tracks { get; set; } = null!;

        public override string ToString()
        {
            return $"{Id} - {Value}";
        }
    }
}
