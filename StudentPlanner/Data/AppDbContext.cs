using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using StudentPlanner.Models;

namespace StudentPlanner.Data;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : IdentityDbContext<ApplicationUser>(options)
{
    public DbSet<Student> Students => Set<Student>();
    public DbSet<Course> Courses => Set<Course>();
    public DbSet<Assignment> Assignments => Set<Assignment>();
    public DbSet<Grade> Grades => Set<Grade>();
    public DbSet<ScheduleEvent> ScheduleEvents => Set<ScheduleEvent>();
    public DbSet<UserPlannerSettings> UserPlannerSettings => Set<UserPlannerSettings>();
    public DbSet<FocusSession> FocusSessions => Set<FocusSession>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.Entity<ApplicationUser>().Property(user => user.FullName).HasMaxLength(100).IsRequired();
        modelBuilder.Entity<UserPlannerSettings>(entity =>
        {
            entity.HasKey(value => value.UserId);
            entity.HasOne<ApplicationUser>().WithOne().HasForeignKey<UserPlannerSettings>(value => value.UserId).OnDelete(DeleteBehavior.Cascade);
        });
        modelBuilder.Entity<FocusSession>(entity =>
        {
            entity.HasKey(session => session.Id);
            entity.Property(session => session.OwnerId).HasMaxLength(450).IsRequired();
            entity.Property(session => session.Type).HasConversion<int>();
            entity.Property(session => session.Status).HasConversion<int>();
            entity.HasIndex(session => new { session.OwnerId, session.StartTime });
            entity.HasIndex(session => session.AssignmentId);
            entity.HasOne<ApplicationUser>().WithMany().HasForeignKey(session => session.OwnerId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(session => session.Assignment).WithMany().HasForeignKey(session => session.AssignmentId).OnDelete(DeleteBehavior.SetNull);
        });
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
            entity.HasIndex(course => course.OwnerId);
            entity.HasOne<ApplicationUser>().WithMany().HasForeignKey(course => course.OwnerId).OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(course => new { course.OwnerId, course.Code }).IsUnique();
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
            entity.HasOne<ApplicationUser>().WithMany().HasForeignKey(assignment => assignment.OwnerId).OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(assignment => assignment.OwnerId);
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
            entity.HasOne<ApplicationUser>().WithMany().HasForeignKey(grade => grade.OwnerId).OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(grade => grade.OwnerId);
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
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<ApplicationUser>().WithMany().HasForeignKey(item => item.OwnerId).OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(item => item.OwnerId);
            entity.HasIndex(item => item.StartTime);
            entity.HasIndex(item => item.CourseId);
        });
    }
}
