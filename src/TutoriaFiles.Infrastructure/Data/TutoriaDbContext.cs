using Microsoft.EntityFrameworkCore;
using TutoriaFiles.Core.Entities;
using FileEntity = TutoriaFiles.Core.Entities.File;

namespace TutoriaFiles.Infrastructure.Data;

public class TutoriaDbContext : DbContext
{
    public TutoriaDbContext(DbContextOptions<TutoriaDbContext> options) : base(options)
    {
    }

    public DbSet<FileEntity> Files { get; set; }
    public DbSet<Module> Modules { get; set; }
    public DbSet<Course> Courses { get; set; }
    public DbSet<ProfessorCourse> ProfessorCourses { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Configure File entity
        modelBuilder.Entity<FileEntity>(entity =>
        {
            entity.ToTable("Files");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(255);
            entity.Property(e => e.FileType).IsRequired().HasMaxLength(50);
            entity.Property(e => e.FileName).HasMaxLength(255);
            entity.Property(e => e.BlobPath).HasMaxLength(500);
            entity.Property(e => e.BlobUrl).HasMaxLength(1000);
            entity.Property(e => e.ContentType).HasMaxLength(100);

            entity.HasOne(e => e.Module)
                .WithMany(m => m.Files)
                .HasForeignKey(e => e.ModuleId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // Configure Module entity
        modelBuilder.Entity<Module>(entity =>
        {
            entity.ToTable("Modules");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(255);

            entity.HasOne(e => e.Course)
                .WithMany(c => c.Modules)
                .HasForeignKey(e => e.CourseId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // Configure Course entity
        modelBuilder.Entity<Course>(entity =>
        {
            entity.ToTable("Courses");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(255);
        });

        // Configure ProfessorCourse entity (many-to-many)
        modelBuilder.Entity<ProfessorCourse>(entity =>
        {
            entity.ToTable("ProfessorCourses");
            entity.HasKey(pc => new { pc.ProfessorId, pc.CourseId });
        });
    }
}
