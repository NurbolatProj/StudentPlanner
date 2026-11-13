using Microsoft.EntityFrameworkCore;
using StudentPlanner.Models;

namespace StudentPlanner.Data;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Student> Students => Set<Student>();
    public DbSet<Course> Courses => Set<Course>();
    public DbSet<Assignment> Assignments => Set<Assignment>();
    public DbSet<Grade> Grades => Set<Grade>();
    public DbSet<ScheduleEvent> ScheduleEvents => Set<ScheduleEvent>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Student>(entity =>
        {
            entity.HasKey(student => student.Id);
            entity.Property(student => student.FullName).HasMaxLength(100).IsRequired();
            entity.Property(student => student.Email).HasMaxLength(254).IsRequired();
            entity.Property(student => student.CreatedAt).IsRequired();
            entity.HasIndex(student => student.Email).IsUnique();
        });

        modelBuilder.Entity<Course>(entity =>
        {
            entity.HasKey(course => course.Id);
            entity.Property(course => course.Name).HasMaxLength(100).IsRequired();
            entity.Property(course => course.Code).HasMaxLength(15).UseCollation("NOCASE").IsRequired();
            entity.Property(course => course.Instructor).HasMaxLength(100).IsRequired();
            entity.Property(course => course.Color).HasMaxLength(16).IsRequired();
            entity.Property(course => course.Description).HasMaxLength(500);
            entity.Property(course => course.Room).HasMaxLength(40);
            entity.Property(course => course.Schedule).HasMaxLength(100);
            entity.HasIndex(course => course.Code).IsUnique();
            entity.HasIndex(course => course.IsActive);
        });

        modelBuilder.Entity<Assignment>(entity =>
        {
            entity.HasKey(assignment => assignment.Id);
            entity.Property(assignment => assignment.Title).HasMaxLength(100).IsRequired();
            entity.Property(assignment => assignment.Description).HasMaxLength(500);
            entity.Property(assignment => assignment.Priority).HasConversion<int>();
            entity.Property(assignment => assignment.Status).HasConversion<int>();
            entity.HasOne(assignment => assignment.Course)
                .WithMany(course => course.Assignments)
                .HasForeignKey(assignment => assignment.CourseId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(assignment => new { assignment.Status, assignment.DueDate });
            entity.HasIndex(assignment => new { assignment.CourseId, assignment.Priority });
        });

        modelBuilder.Entity<Grade>(entity =>
        {
            entity.HasKey(grade => grade.Id);
            entity.Property(grade => grade.Title).HasMaxLength(100).IsRequired();
            entity.Property(grade => grade.Score).HasPrecision(9, 2);
            entity.Property(grade => grade.MaxScore).HasPrecision(9, 2);
            entity.Property(grade => grade.Weight).HasPrecision(7, 3);
            entity.HasOne(grade => grade.Course)
                .WithMany(course => course.Grades)
                .HasForeignKey(grade => grade.CourseId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(grade => new { grade.CourseId, grade.Date });
        });

        modelBuilder.Entity<ScheduleEvent>(entity =>
        {
            entity.HasKey(item => item.Id);
            entity.Property(item => item.Title).HasMaxLength(100).IsRequired();
            entity.Property(item => item.Description).HasMaxLength(500);
            entity.Property(item => item.Location).HasMaxLength(100);
            entity.Property(item => item.Type).HasConversion<int>();
            entity.HasOne(item => item.Course)
                .WithMany(course => course.ScheduleEvents)
                .HasForeignKey(item => item.CourseId)
                .OnDelete(DeleteBehavior.SetNull);
            entity.HasIndex(item => item.StartTime);
            entity.HasIndex(item => item.CourseId);
        });
    }
}
