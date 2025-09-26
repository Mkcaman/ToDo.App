using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ToDo.Domain
{
    public class TodoItem
    {
        public int Id { get; set; }

        [Required] public string OwnerId { get; set; } = default!;
        public ApplicationUser? Owner { get; set; }

        [Required, StringLength(200)]
        public string Title { get; set; } = "";

        // ZORUNLU
        [Required]
        public string Description { get; set; } = "";

        // ZORUNLU (UTC önerilir) — artık nullable değil
        [Required]
        public DateTime DueAt { get; set; }

        public bool IsDone { get; set; }

        /// <summary>0–100 öncelik</summary>
        [Range(0, 100)]
        public int Priority { get; set; } = 50;

        /// <summary>Oluşturulma (atanma) zamanı</summary>
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Tek dosyalı senaryoda kullanacağımız web yolu
        /// (örn: /uploads/tasks/{userId}/file.ext)
        /// Çoklu dosyaya geçince ayrı tabloya taşıyacağız.
        /// </summary>
        [StringLength(256)]
        public string? AttachmentPath { get; set; }

        /// <summary>Şu an itibarıyla gecikmiş mi? (DB’ye yazılmaz)</summary>
        [NotMapped]
        public bool IsOverdueNow => !IsDone && DueAt < DateTime.UtcNow;
        public ICollection<TaskAttachment> Attachments { get; set; } = new List<TaskAttachment>();

    }
}

