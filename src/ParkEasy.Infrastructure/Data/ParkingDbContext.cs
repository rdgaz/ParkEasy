using Microsoft.EntityFrameworkCore;
using ParkEasy.Domain.Entities;

namespace ParkEasy.Infrastructure.Data;

public class ParkingDbContext : DbContext
{
    public ParkingDbContext(DbContextOptions<ParkingDbContext> options) : base(options)
    {
    }

    public DbSet<ParkingSession> ParkingSessions { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<ParkingSession>(entity =>
        {
            entity.ToTable("ParkingSessions");

            entity.HasKey(e => e.Id);

            entity.Property(e => e.Id)
                .ValueGeneratedOnAdd();

            entity.Property(e => e.TicketNumber)
                .IsRequired()
                .HasMaxLength(10);

            entity.Property(e => e.Plate)
                .IsRequired()
                .HasMaxLength(10);

            entity.Property(e => e.VehicleModel)
                .HasMaxLength(100);

            entity.Property(e => e.CustomerName)
                .HasMaxLength(200);

            entity.Property(e => e.CustomerPhone)
                .HasMaxLength(20);

            entity.Property(e => e.FinalAmount)
                .HasColumnType("REAL");

            entity.Property(e => e.Status)
                .IsRequired();

            // Indexes
            entity.HasIndex(e => e.TicketNumber)
                .IsUnique();

            entity.HasIndex(e => e.Plate);

            entity.HasIndex(e => e.Status);

            entity.HasIndex(e => e.EntryDateTime);
        });
    }
}
