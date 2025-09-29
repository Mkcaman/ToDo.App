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
        public DbSet<TaskComment> TaskComments => Set<TaskComment>(); // yorumlar
        public DbSet<TaskCommentAttachment> TaskCommentAttachments => Set<TaskCommentAttachment>();

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

                // Controller boşta null set ettiği için opsiyonel
                e.Property(t => t.Description)
                 .HasMaxLength(4000)
                 .IsRequired(false);

                e.Property(t => t.DueAt).IsRequired();

                e.Property(t => t.AttachmentPath).HasMaxLength(256);

                e.Property(t => t.Priority).HasDefaultValue(50);

                e.ToTable(tb =>
                {
                    tb.HasCheckConstraint("CK_TodoItem_Priority_0_100", "\"Priority\" BETWEEN 0 AND 100");
                });

                e.Property(t => t.CreatedAt)
                 .HasDefaultValueSql("timezone('utc', now())");

                e.HasOne(t => t.Owner)
                 .WithMany()
                 .HasForeignKey(t => t.OwnerId)
                 .OnDelete(DeleteBehavior.Cascade);

                e.HasIndex(t => new { t.OwnerId, t.IsDone, t.DueAt });
            });

            // ---------- TaskAttachment ----------
            b.Entity<TaskAttachment>(e =>
            {
                e.HasKey(x => x.Id);

                e.Property(x => x.FileTitle).HasMaxLength(120);
                e.Property(x => x.FileNameOriginal).HasMaxLength(200).IsRequired();
                e.Property(x => x.Url).HasMaxLength(256).IsRequired();
                e.Property(x => x.ContentType).HasMaxLength(100);

                e.Property(x => x.CreatedAt)
                 .HasDefaultValueSql("timezone('utc', now())");

                e.HasOne(x => x.TodoItem)
                 .WithMany(t => t.Attachments)
                 .HasForeignKey(x => x.TodoItemId)
                 .OnDelete(DeleteBehavior.Cascade);

                e.HasIndex(x => x.TodoItemId);
            });


            // ---------- TaskComment (dosya destekli) ----------
            b.Entity<TaskComment>(e =>
            {
                e.HasKey(c => c.Id);

                e.Property(c => c.Body).HasMaxLength(4000).IsRequired();
                e.Property(c => c.CreatedAt).HasDefaultValueSql("timezone('utc', now())");

                // Yorum eki (opsiyonel)
                e.Property(c => c.FileUrl).HasMaxLength(256);
                e.Property(c => c.FileContentType).HasMaxLength(100);
                // FileSizeBytes long → ekstra config gerekmez

                e.HasOne(c => c.TodoItem)
                 .WithMany(t => t.Comments)
                 .HasForeignKey(c => c.TodoItemId)
                 .OnDelete(DeleteBehavior.Cascade);

                e.HasOne(c => c.User)
                 .WithMany()
                 .HasForeignKey(c => c.UserId)
                 .OnDelete(DeleteBehavior.Cascade);

                e.HasIndex(c => new { c.TodoItemId, c.CreatedAt });
            });

            // ---------- TaskCommentAttachment (çouklu dosya) ----------
            // OnModelCreating içinde TaskCommentAttachment map'ine ekle:
            b.Entity<TaskCommentAttachment>(e =>
            {
                e.HasKey(x => x.Id);
                e.Property(x => x.Url).HasMaxLength(256).IsRequired();
                e.Property(x => x.ContentType).HasMaxLength(100);
                e.Property(x => x.CreatedAt).HasDefaultValueSql("timezone('utc', now())");

                e.Property(x => x.FileTitle).HasMaxLength(120); // <-- YENİ

                e.HasOne(x => x.TaskComment)
                 .WithMany(c => c.Attachments)
                 .HasForeignKey(x => x.TaskCommentId)
                 .OnDelete(DeleteBehavior.Cascade);

                e.HasIndex(x => new { x.TaskCommentId, x.CreatedAt });
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
