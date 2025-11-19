using Microsoft.EntityFrameworkCore;
using MindWork.Api.Domain.Entities;

namespace MindWork.Api.Infrastructure.Persistence;

public class MindWorkDbContext : DbContext
{
    public MindWorkDbContext(DbContextOptions<MindWorkDbContext> options)
        : base(options)
    {
    }

    public DbSet<User> Users => Set<User>();
    public DbSet<SelfAssessment> SelfAssessments => Set<SelfAssessment>();
    public DbSet<WellnessEvent> WellnessEvents => Set<WellnessEvent>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
      
        modelBuilder.Entity<User>(entity =>
        {
            entity.ToTable("Users");

            entity.HasKey(u => u.Id);

            entity.Property(u => u.FullName)
                .IsRequired()
                .HasMaxLength(200);

            entity.Property(u => u.Email)
                .IsRequired()
                .HasMaxLength(200);

            entity.HasIndex(u => u.Email)
                .IsUnique();

            entity.Property(u => u.PasswordHash)
                .IsRequired()
                .HasMaxLength(500);

            entity.Property(u => u.Role)
                .IsRequired();

            entity.Property(u => u.CreatedAt)
                .IsRequired();

            entity.Property(u => u.IsActive)
                .HasDefaultValue(true);
        });
      
        modelBuilder.Entity<SelfAssessment>(entity =>
        {
            entity.ToTable("SelfAssessments");

            entity.HasKey(sa => sa.Id);

            entity.Property(sa => sa.CreatedAt)
                .IsRequired();

            entity.Property(sa => sa.Notes)
                .HasMaxLength(1000);

            entity
                .HasOne(sa => sa.User)
                .WithMany()
                .HasForeignKey(sa => sa.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });
      
        modelBuilder.Entity<WellnessEvent>(entity =>
        {
            entity.ToTable("WellnessEvents");

            entity.HasKey(we => we.Id);

            entity.Property(we => we.EventType)
                .IsRequired()
                .HasMaxLength(100);

            entity.Property(we => we.Source)
                .IsRequired()
                .HasMaxLength(100);

            entity.Property(we => we.OccurredAt)
                .IsRequired();

            entity.Property(we => we.MetadataJson)
                .HasColumnType("nvarchar(max)");

            entity
                .HasOne<User>()
                .WithMany()
                .HasForeignKey(we => we.UserId)
                .OnDelete(DeleteBehavior.SetNull);
        });
    }
}
