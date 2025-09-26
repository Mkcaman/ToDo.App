using System;
using System.ComponentModel.DataAnnotations;

namespace ToDo.Domain
{
    public class TaskAttachment
    {
        public int Id { get; set; }

        [Required]
        public int TodoItemId { get; set; }
        public TodoItem TodoItem { get; set; } = default!;

        /// <summary>Kullanıcının verdiği görünen ad (isteğe bağlı değilse Required ekleyebilirsin)</summary>
        [StringLength(120)]
        public string? FileTitle { get; set; }

        /// <summary>Yüklenen dosyanın orijinal adı</summary>
        [Required, StringLength(200)]
        public string FileNameOriginal { get; set; } = "";

        /// <summary>wwwroot içi web yolu (örn: /uploads/tasks/{userId}/{taskId}/{guid.ext})</summary>
        [Required, StringLength(256)]
        public string Url { get; set; } = "";

        [StringLength(100)]
        public string? ContentType { get; set; }

        public long SizeBytes { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}

