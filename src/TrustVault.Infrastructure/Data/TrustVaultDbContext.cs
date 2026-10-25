using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using TrustVault.Core.Entities;

namespace TrustVault.Infrastructure.Data;

public class TrustVaultDbContext : IdentityDbContext<ApplicationUser, IdentityRole<Guid>, Guid>
{
    public TrustVaultDbContext(DbContextOptions<TrustVaultDbContext> options)
        : base(options)
    {
    }

    public DbSet<Document> Documents => Set<Document>();
    public DbSet<DocumentVersion> DocumentVersions => Set<DocumentVersion>();
    public DbSet<DocumentGrant> DocumentGrants => Set<DocumentGrant>();
    public DbSet<AuditEvent> AuditEvents => Set<AuditEvent>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        // Document configuration
        builder.Entity<Document>(entity =>
        {
            entity.HasKey(d => d.Id);
            entity.Property(d => d.Title).IsRequired().HasMaxLength(200);
            entity.Property(d => d.OriginalFileName).IsRequired().HasMaxLength(255);
            entity.Property(d => d.Category).IsRequired().HasMaxLength(100);
            entity.Property(d => d.Tags).HasMaxLength(200);

            entity.HasOne(d => d.Owner)
                .WithMany(u => u.OwnedDocuments)
                .HasForeignKey(d => d.OwnerId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasIndex(d => d.OwnerId);
            entity.HasIndex(d => d.Status);
            entity.HasIndex(d => d.Category);
            entity.HasIndex(d => d.CreatedAtUtc);
        });

        // DocumentVersion configuration
        builder.Entity<DocumentVersion>(entity =>
        {
            entity.HasKey(v => v.Id);
            entity.Property(v => v.StorageKey).IsRequired().HasMaxLength(255);
            entity.Property(v => v.ContentType).IsRequired().HasMaxLength(100);
            entity.Property(v => v.Sha256Hash).IsRequired().HasMaxLength(64);

            entity.HasOne(v => v.Document)
                .WithMany(d => d.Versions)
                .HasForeignKey(v => v.DocumentId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(v => v.UploadedByUser)
                .WithMany()
                .HasForeignKey(v => v.UploadedByUserId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasIndex(v => new { v.DocumentId, v.VersionNumber }).IsUnique();
        });

        // DocumentGrant configuration
        builder.Entity<DocumentGrant>(entity =>
        {
            entity.HasKey(g => g.Id);

            entity.HasOne(g => g.Document)
                .WithMany(d => d.Grants)
                .HasForeignKey(g => g.DocumentId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(g => g.User)
                .WithMany(u => u.ReceivedGrants)
                .HasForeignKey(g => g.UserId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(g => g.GrantedByUser)
                .WithMany(u => u.IssuedGrants)
                .HasForeignKey(g => g.GrantedByUserId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasIndex(g => new { g.DocumentId, g.UserId, g.IsActive });
            entity.HasIndex(g => g.UserId);
        });

        // AuditEvent configuration
        builder.Entity<AuditEvent>(entity =>
        {
            entity.HasKey(a => a.Id);
            entity.Property(a => a.Id).ValueGeneratedOnAdd();
            entity.Property(a => a.ActorId).IsRequired().HasMaxLength(100);
            entity.Property(a => a.Action).IsRequired().HasMaxLength(100);
            entity.Property(a => a.Result).IsRequired().HasMaxLength(50);
            entity.Property(a => a.ResourceId).HasMaxLength(100);
            entity.Property(a => a.ClientIp).HasMaxLength(50);
            entity.Property(a => a.CorrelationId).HasMaxLength(100);
            entity.Property(a => a.PreviousHash).IsRequired().HasMaxLength(64);
            entity.Property(a => a.CurrentHash).IsRequired().HasMaxLength(64);

            entity.HasIndex(a => a.TimestampUtc);
            entity.HasIndex(a => a.Action);
            entity.HasIndex(a => a.ActorId);
            entity.HasIndex(a => a.ResourceId);
        });
    }
}
