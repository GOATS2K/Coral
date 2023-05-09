using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Coral.Services.Models
{
    public class PaginatedCustomData<TType> 
        where TType : class
    {
        public int AvailableRecords { get; init; }
        public int TotalRecords { get; init; }
        public int ResultCount { get; init; }
        public TType Data { get; init; } = null!;
    }
}
