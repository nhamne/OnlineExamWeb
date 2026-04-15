using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;

namespace OnlineExam.Models;

public partial class OnlineExamDbContext : DbContext
{
    public OnlineExamDbContext()
    {
    }

    public OnlineExamDbContext(DbContextOptions<OnlineExamDbContext> options)
        : base(options)
    {
    }

    public virtual DbSet<Classroom> Classrooms { get; set; }

    public virtual DbSet<ClassroomMember> ClassroomMembers { get; set; }

    public virtual DbSet<ExamPaper> ExamPapers { get; set; }

    public virtual DbSet<ExamSession> ExamSessions { get; set; }

    public virtual DbSet<GiaoDich> GiaoDiches { get; set; }

    public virtual DbSet<Question> Questions { get; set; }

    public virtual DbSet<Submission> Submissions { get; set; }

    public virtual DbSet<SubmissionDetail> SubmissionDetails { get; set; }

    public virtual DbSet<TaiKhoan> TaiKhoans { get; set; }

    public virtual DbSet<User> Users { get; set; }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
#warning To protect potentially sensitive information in your connection string, you should move it out of source code. You can avoid scaffolding the connection string by using the Name= syntax to read it from configuration - see https://go.microsoft.com/fwlink/?linkid=2131148. For more guidance on storing connection strings, see https://go.microsoft.com/fwlink/?LinkId=723263.
        => optionsBuilder.UseSqlServer("Server=localhost\\SQLEXPRESS;Database=OnlineExamDb;Trusted_Connection=True;TrustServerCertificate=True");

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Classroom>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__Classroo__3214EC0733C00F26");

            entity.HasIndex(e => e.JoinCode, "UQ__Classroo__FF7C6BA0F4B07EE5").IsUnique();

            entity.Property(e => e.ClassName).HasMaxLength(255);
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime");
            entity.Property(e => e.IsDeleted).HasDefaultValue(false);
            entity.Property(e => e.JoinCode)
                .HasMaxLength(20)
                .IsUnicode(false);

            entity.HasOne(d => d.Teacher).WithMany(p => p.Classrooms)
                .HasForeignKey(d => d.TeacherId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__Classroom__Teach__5070F446");
        });

        modelBuilder.Entity<ClassroomMember>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__Classroo__3214EC074E8827D8");

            entity.Property(e => e.JoinedAt)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime");

            entity.HasOne(d => d.Classroom).WithMany(p => p.ClassroomMembers)
                .HasForeignKey(d => d.ClassroomId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__Classroom__Class__5535A963");

            entity.HasOne(d => d.Student).WithMany(p => p.ClassroomMembers)
                .HasForeignKey(d => d.StudentId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__Classroom__Stude__5629CD9C");
        });

        modelBuilder.Entity<ExamPaper>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__ExamPape__3214EC071EF2CA83");

            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime");
            entity.Property(e => e.IsDeleted).HasDefaultValue(false);
            entity.Property(e => e.Title).HasMaxLength(255);

            entity.HasOne(d => d.Teacher).WithMany(p => p.ExamPapers)
                .HasForeignKey(d => d.TeacherId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__ExamPaper__Teach__59FA5E80");
        });

        modelBuilder.Entity<ExamSession>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__ExamSess__3214EC07CEFD9748");

            entity.Property(e => e.AllowViewScore).HasDefaultValue(true);
            entity.Property(e => e.EndTime).HasColumnType("datetime");
            entity.Property(e => e.IsShuffled).HasDefaultValue(true);
            entity.Property(e => e.SessionName).HasMaxLength(255);
            entity.Property(e => e.SessionPassword)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.StartTime).HasColumnType("datetime");

            entity.HasOne(d => d.Classroom).WithMany(p => p.ExamSessions)
                .HasForeignKey(d => d.ClassroomId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__ExamSessi__Class__628FA481");

            entity.HasOne(d => d.ExamPaper).WithMany(p => p.ExamSessions)
                .HasForeignKey(d => d.ExamPaperId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__ExamSessi__ExamP__6383C8BA");
        });

        modelBuilder.Entity<GiaoDich>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__GiaoDich__3214EC271158D83D");

            entity.ToTable("GiaoDich");

            entity.Property(e => e.Id).HasColumnName("ID");
            entity.Property(e => e.Loai).HasMaxLength(50);
            entity.Property(e => e.MoTa).HasMaxLength(500);
            entity.Property(e => e.SoTkgui)
                .HasMaxLength(20)
                .IsUnicode(false)
                .HasColumnName("SoTKGui");
            entity.Property(e => e.SoTknhan)
                .HasMaxLength(20)
                .IsUnicode(false)
                .HasColumnName("SoTKNhan");
            entity.Property(e => e.ThoiGian)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime");
        });

        modelBuilder.Entity<Question>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__Question__3214EC0749DBE95F");

            entity.Property(e => e.CorrectOption)
                .HasMaxLength(1)
                .IsUnicode(false)
                .IsFixedLength();

            entity.HasOne(d => d.ExamPaper).WithMany(p => p.Questions)
                .HasForeignKey(d => d.ExamPaperId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__Questions__ExamP__5EBF139D");
        });

        modelBuilder.Entity<Submission>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__Submissi__3214EC0755C6F3ED");

            entity.Property(e => e.StartedAt).HasColumnType("datetime");
            entity.Property(e => e.Status).HasDefaultValue(0);
            entity.Property(e => e.SubmittedAt).HasColumnType("datetime");
            entity.Property(e => e.WarningCount).HasDefaultValue(0);

            entity.HasOne(d => d.ExamSession).WithMany(p => p.Submissions)
                .HasForeignKey(d => d.ExamSessionId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__Submissio__ExamS__68487DD7");

            entity.HasOne(d => d.Student).WithMany(p => p.Submissions)
                .HasForeignKey(d => d.StudentId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__Submissio__Stude__693CA210");
        });

        modelBuilder.Entity<SubmissionDetail>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__Submissi__3214EC07F26E38DC");

            entity.Property(e => e.SelectedOption)
                .HasMaxLength(1)
                .IsUnicode(false)
                .IsFixedLength();

            entity.HasOne(d => d.Question).WithMany(p => p.SubmissionDetails)
                .HasForeignKey(d => d.QuestionId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__Submissio__Quest__6EF57B66");

            entity.HasOne(d => d.Submission).WithMany(p => p.SubmissionDetails)
                .HasForeignKey(d => d.SubmissionId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__Submissio__Submi__6E01572D");
        });

        modelBuilder.Entity<TaiKhoan>(entity =>
        {
            entity.HasKey(e => e.SoTk).HasName("PK__TaiKhoan__BC3C8AF3156F2122");

            entity.ToTable("TaiKhoan");

            entity.Property(e => e.SoTk)
                .HasMaxLength(20)
                .IsUnicode(false)
                .HasColumnName("SoTK");
            entity.Property(e => e.HoTen).HasMaxLength(200);
        });

        modelBuilder.Entity<User>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__Users__3214EC0714F00E4B");

            entity.HasIndex(e => e.Email, "UQ__Users__A9D105340F163118").IsUnique();

            entity.Property(e => e.Email)
                .HasMaxLength(255)
                .IsUnicode(false);
            entity.Property(e => e.FullName).HasMaxLength(255);
            entity.Property(e => e.IsActive).HasDefaultValue(true);
            entity.Property(e => e.PasswordHash)
                .HasMaxLength(255)
                .IsUnicode(false);
            entity.Property(e => e.Role)
                .HasMaxLength(50)
                .IsUnicode(false);
        });

        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}
