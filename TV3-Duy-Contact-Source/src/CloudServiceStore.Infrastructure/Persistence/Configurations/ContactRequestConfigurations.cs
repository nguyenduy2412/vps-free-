using CloudServiceStore.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CloudServiceStore.Infrastructure.Persistence.Configurations;

public sealed class ContactRequestConfiguration : IEntityTypeConfiguration<ContactRequest>
{
    public void Configure(EntityTypeBuilder<ContactRequest> builder)
    {
        builder.ToTable("ContactRequests");

        builder.Property(x => x.FullName)
            .HasMaxLength(160)
            .IsRequired();

        builder.Property(x => x.Email)
            .HasMaxLength(256)
            .IsRequired();

        builder.Property(x => x.PhoneNumber)
            .HasMaxLength(30)
            .IsRequired();

        builder.Property(x => x.CompanyName)
            .HasMaxLength(160);

        builder.Property(x => x.Subject)
            .HasMaxLength(180)
            .IsRequired();

        builder.Property(x => x.Message)
            .HasMaxLength(4000)
            .IsRequired();

        builder.Property(x => x.ResolutionNote)
            .HasMaxLength(1000);

        // Supports default list sorting, status queue filtering, owner lookup,
        // and the normalized-email duplicate window respectively.
        builder.HasIndex(x => x.CreatedAt)
            .IsDescending();
        builder.HasIndex(x => new { x.Status, x.CreatedAt })
            .IsDescending(false, true);
        builder.HasIndex(x => new { x.AppUserId, x.CreatedAt })
            .IsDescending(false, true);
        builder.HasIndex(x => new { x.Email, x.CreatedAt })
            .IsDescending(false, true);

        builder.HasOne(x => x.AppUser)
            .WithMany()
            .HasForeignKey(x => x.AppUserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class ContactRequestStatusHistoryConfiguration
    : IEntityTypeConfiguration<ContactRequestStatusHistory>
{
    public void Configure(EntityTypeBuilder<ContactRequestStatusHistory> builder)
    {
        builder.ToTable("ContactRequestStatusHistories");

        builder.Property(x => x.Note)
            .HasMaxLength(1000);

        builder.HasIndex(x => new { x.ContactRequestId, x.CreatedAt })
            .IsDescending(false, true);

        builder.HasOne(x => x.ContactRequest)
            .WithMany(x => x.StatusHistory)
            .HasForeignKey(x => x.ContactRequestId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
