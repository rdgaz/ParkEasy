using Microsoft.EntityFrameworkCore;
using ParkEasy.Domain.Entities;

namespace ParkEasy.Infrastructure.Data;

public class ParkingDbContext : DbContext
{
    public ParkingDbContext(DbContextOptions<ParkingDbContext> options) : base(options)
    {
    }

    public DbSet<ParkingSession> ParkingSessions { get; set; } = null!;
    public DbSet<User> Users { get; set; } = null!;

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

            entity.Property(e => e.VehicleType)
                .IsRequired();

            entity.Property(e => e.VehicleModel)
                .HasMaxLength(100);

            entity.Property(e => e.CustomerName)
                .HasMaxLength(200);

            entity.Property(e => e.CustomerPhone)
                .HasMaxLength(20);

            entity.Property(e => e.FinalAmount)
                .HasColumnType("REAL");

            entity.Property(e => e.EntryUsername)
                .HasMaxLength(50);

            entity.Property(e => e.CheckoutUsername)
                .HasMaxLength(50);

            entity.Property(e => e.ServiceType)
                .HasMaxLength(100);

            entity.Property(e => e.ServiceAmount)
                .HasColumnType("REAL");

            entity.Property(e => e.ServiceNotes)
                .HasMaxLength(300);

            entity.Property(e => e.ServiceStatus);

            entity.Property(e => e.ServiceRequestedAt);

            entity.Property(e => e.SyncedToSheets)
                .IsRequired();

            entity.Property(e => e.Status)
                .IsRequired();

            // Indexes
            entity.HasIndex(e => e.TicketNumber)
                .IsUnique();

            entity.HasIndex(e => e.Plate);

            entity.HasIndex(e => e.Status);

            entity.HasIndex(e => e.EntryDateTime);
        });

        modelBuilder.Entity<User>(entity =>
        {
            entity.ToTable("Users");

            entity.HasKey(e => e.Id);

            entity.Property(e => e.Id)
                .ValueGeneratedOnAdd();

            entity.Property(e => e.Username)
                .IsRequired()
                .HasMaxLength(50);

            entity.Property(e => e.PasswordHash)
                .IsRequired()
                .HasMaxLength(100);

            entity.Property(e => e.PasswordSalt)
                .IsRequired()
                .HasMaxLength(50);

            entity.Property(e => e.Role)
                .IsRequired();

            entity.HasIndex(e => e.Username)
                .IsUnique();
        });
    }
}
