using Coral.Database.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Coral.Database.Configurations
{
    public class KeywordConfiguration : IEntityTypeConfiguration<Keyword>
    {
        public void Configure(EntityTypeBuilder<Keyword> builder)
        {
            builder.Property(p => p.Value).IsRequired();
            builder.HasIndex(p => p.Value);
            builder.ToTable(nameof(Keyword));
        }
    }
}
