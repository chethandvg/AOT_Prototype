using Microsoft.EntityFrameworkCore;
using PropertyManagement.Models;

namespace PropertyManagement.API.Data;

public class PropertyManagementDbContext : DbContext
{
    public PropertyManagementDbContext(DbContextOptions<PropertyManagementDbContext> options)
        : base(options)
    {
    }

    public DbSet<Building> Buildings => Set<Building>();
    public DbSet<Unit> Units => Set<Unit>();
    public DbSet<Owner> Owners => Set<Owner>();
    public DbSet<BuildingOwnershipShare> BuildingOwnershipShares => Set<BuildingOwnershipShare>();
    public DbSet<UnitOwnershipShare> UnitOwnershipShares => Set<UnitOwnershipShare>();
    public DbSet<BuildingFile> BuildingFiles => Set<BuildingFile>();
    public DbSet<UnitFile> UnitFiles => Set<UnitFile>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Building configuration
        modelBuilder.Entity<Building>(entity =>
        {
            entity.HasKey(b => b.Id);
            entity.HasIndex(b => new { b.OrganizationId, b.Code }).IsUnique();
            entity.OwnsOne(b => b.Address);
        });

        // Unit configuration
        modelBuilder.Entity<Unit>(entity =>
        {
            entity.HasKey(u => u.Id);
            entity.HasIndex(u => new { u.BuildingId, u.UnitNumber }).IsUnique();
            entity.HasOne(u => u.Building)
                  .WithMany(b => b.Units)
                  .HasForeignKey(u => u.BuildingId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        // Owner configuration
        modelBuilder.Entity<Owner>(entity =>
        {
            entity.HasKey(o => o.Id);
            entity.HasIndex(o => new { o.OrganizationId, o.Email });
        });

        // BuildingOwnershipShare configuration
        modelBuilder.Entity<BuildingOwnershipShare>(entity =>
        {
            entity.HasKey(s => s.Id);
            entity.HasOne(s => s.Building)
                  .WithMany(b => b.OwnershipShares)
                  .HasForeignKey(s => s.BuildingId)
                  .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(s => s.Owner)
                  .WithMany(o => o.BuildingShares)
                  .HasForeignKey(s => s.OwnerId)
                  .OnDelete(DeleteBehavior.Restrict);
            entity.Property(s => s.SharePercent).HasPrecision(5, 2);
        });

        // UnitOwnershipShare configuration
        modelBuilder.Entity<UnitOwnershipShare>(entity =>
        {
            entity.HasKey(s => s.Id);
            entity.HasOne(s => s.Unit)
                  .WithMany(u => u.OwnershipShares)
                  .HasForeignKey(s => s.UnitId)
                  .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(s => s.Owner)
                  .WithMany(o => o.UnitShares)
                  .HasForeignKey(s => s.OwnerId)
                  .OnDelete(DeleteBehavior.Restrict);
            entity.Property(s => s.SharePercent).HasPrecision(5, 2);
        });

        // BuildingFile configuration
        modelBuilder.Entity<BuildingFile>(entity =>
        {
            entity.HasKey(f => f.Id);
            entity.HasOne(f => f.Building)
                  .WithMany(b => b.Files)
                  .HasForeignKey(f => f.BuildingId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        // UnitFile configuration
        modelBuilder.Entity<UnitFile>(entity =>
        {
            entity.HasKey(f => f.Id);
            entity.HasOne(f => f.Unit)
                  .WithMany(u => u.Files)
                  .HasForeignKey(f => f.UnitId)
                  .OnDelete(DeleteBehavior.Cascade);
        });
    }
}
