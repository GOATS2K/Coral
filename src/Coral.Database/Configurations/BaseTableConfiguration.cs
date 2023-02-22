using Coral.Database.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Coral.Database.Configurations
{
    public class BaseTableConfiguration : IEntityTypeConfiguration<BaseTable>
    {
        public void Configure(EntityTypeBuilder<BaseTable> builder)
        {
            builder.Property(b => b.DateModified)
                .HasDefaultValue(DateTime.UtcNow)
                .ValueGeneratedOnUpdate();

            builder.Property(b => b.DateIndexed)
                .HasDefaultValue(DateTime.UtcNow)
                .ValueGeneratedOnAdd();
        }
    }
}
