using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Persistence.Configurations;

/// <summary>
/// EF Core configuration for Config entity.
/// </summary>
public class ConfigConfiguration : IEntityTypeConfiguration<Config>
{
    public void Configure(EntityTypeBuilder<Config> builder)
    {
        builder.ToTable("Configs");

        builder.HasKey(c => c.Id);

        builder.Property(c => c.Key)
            .IsRequired()
            .HasMaxLength(200);

        builder.HasIndex(c => c.Key)
            .IsUnique()
            .HasDatabaseName("IX_Configs_Key");

        builder.Property(c => c.Value)
            .IsRequired()
            .HasMaxLength(2000);

        builder.Property(c => c.Description)
            .HasMaxLength(500);

        builder.Property(c => c.Category)
            .HasMaxLength(100)
            .HasDefaultValue("General");

        builder.Property(c => c.IsActive)
            .IsRequired()
            .HasDefaultValue(true);

        builder.Property(c => c.CreatedAt)
            .IsRequired()
            .HasDefaultValueSql("CURRENT_TIMESTAMP");

        builder.Property(c => c.ModifiedAt)
            .IsRequired(false);

        builder.Property(c => c.IsDeleted)
            .IsRequired()
            .HasDefaultValue(false);

        builder.HasIndex(c => c.IsDeleted)
            .HasDatabaseName("IX_Configs_IsDeleted");

        builder.HasIndex(c => c.Category)
            .HasDatabaseName("IX_Configs_Category");
    }
}
