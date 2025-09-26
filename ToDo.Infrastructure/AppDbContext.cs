using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using ToDo.Domain;

namespace ToDo.Infrastructure
{
    public class AppDbContext : IdentityDbContext<ApplicationUser>
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

        // --- Uygulama tabloları ---
        public DbSet<TodoItem> TodoItems => Set<TodoItem>();
        public DbSet<TaskAttachment> TaskAttachments => Set<TaskAttachment>();
        public DbSet<Notification> Notifications => Set<Notification>();


        protected override void OnModelCreating(ModelBuilder b)
        {
            base.OnModelCreating(b);

            // ---------- ApplicationUser ----------
            b.Entity<ApplicationUser>(e =>
            {
                e.Property(x => x.FirstName).HasMaxLength(50);
                e.Property(x => x.LastName).HasMaxLength(50);
                e.Property(x => x.AboutText).HasMaxLength(4000);
                e.Property(x => x.ProfilePhotoPath).HasMaxLength(256);
                e.Property(x => x.AboutDocPath).HasMaxLength(256);

                // Telefon: tekil (NULL değerler hariç)
                e.HasIndex(x => x.PhoneNumber)
                 .IsUnique()
                 .HasFilter("\"PhoneNumber\" IS NOT NULL");
            });

            // ---------- TodoItem ----------
            b.Entity<TodoItem>(e =>
            {
                e.HasKey(t => t.Id);

                e.Property(t => t.Title)
                 .HasMaxLength(200)
                 .IsRequired();

                e.Property(t => t.Description)
                 .HasMaxLength(4000)
                 .IsRequired();

                e.Property(t => t.DueAt)
                 .IsRequired();

                // Tek dosya (eski alan) – opsiyonel
                e.Property(t => t.AttachmentPath)
                 .HasMaxLength(256);

                // Priority: 0..100, varsayılan 50
                e.Property(t => t.Priority)
                 .HasDefaultValue(50);

                e.ToTable(tb =>
                {
                    tb.HasCheckConstraint(
                        "CK_TodoItem_Priority_0_100",
                        "\"Priority\" BETWEEN 0 AND 100"
                    );
                });

                // UTC varsayılan
                e.Property(t => t.CreatedAt)
                 .HasDefaultValueSql("timezone('utc', now())");

                e.HasOne(t => t.Owner)
                 .WithMany()
                 .HasForeignKey(t => t.OwnerId)
                 .OnDelete(DeleteBehavior.Cascade);

                // Sık kullanımlı indeks
                e.HasIndex(t => new { t.OwnerId, t.IsDone, t.DueAt });
            });

            // ---------- TaskAttachment ----------
            b.Entity<TaskAttachment>(e =>
            {
                e.HasKey(x => x.Id);

                e.Property(x => x.FileTitle)
                 .HasMaxLength(120);

                e.Property(x => x.FileNameOriginal)
                 .HasMaxLength(200)
                 .IsRequired();

                e.Property(x => x.Url)
                 .HasMaxLength(256)
                 .IsRequired();

                e.Property(x => x.ContentType)
                 .HasMaxLength(100);

                // UTC varsayılan
                e.Property(x => x.CreatedAt)
                 .HasDefaultValueSql("timezone('utc', now())");

                e.HasOne(x => x.TodoItem)
                 .WithMany(t => t.Attachments) // TodoItem içinde ICollection<TaskAttachment> Attachments olmalı
                 .HasForeignKey(x => x.TodoItemId)
                 .OnDelete(DeleteBehavior.Cascade);

                e.HasIndex(x => x.TodoItemId);
            });

            // ---------- Notification ----------
            b.Entity<Notification>(e =>
            {
                e.HasKey(n => n.Id);

                e.Property(n => n.Title).HasMaxLength(200).IsRequired();
                e.Property(n => n.Message).HasMaxLength(4000);
                e.Property(n => n.Url).HasMaxLength(256);

                e.Property(n => n.CreatedAt)
                 .HasDefaultValueSql("timezone('utc', now())");

                e.HasOne(n => n.User)
                 .WithMany()
                 .HasForeignKey(n => n.UserId)
                 .OnDelete(DeleteBehavior.Cascade);

                e.HasOne(n => n.TodoItem)
                 .WithMany()
                 .HasForeignKey(n => n.TodoItemId)
                 .OnDelete(DeleteBehavior.SetNull);

                e.HasIndex(n => new { n.UserId, n.IsArchived, n.IsRead, n.CreatedAt });
            });


        }
    }
}
