namespace Coral.Services.Models
{
    public class PaginatedQuery<TType> 
        where TType : class
    {
        public int AvailableRecords { get; init; }
        public int TotalRecords { get; init; }
        public int ResultCount { get; init; }
        public List<TType> Data { get; init; } = null!;
    }
}
