namespace Coral.Database.Models
{
    public abstract class BaseTable
    {
        public int Id { get; set; }
        public DateTime DateIndexed { get; set; }
        public DateTime DateModified { get; set; }
    }
}
