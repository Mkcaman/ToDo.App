using System;
using System.ComponentModel.DataAnnotations;

namespace ToDo.Domain
{
    public enum NotificationType
    {
        TaskAssigned = 1,
        TaskDueSoon = 2,
        TaskOverdue = 3,
        System = 99
    }

    public class Notification
    {
        public int Id { get; set; }

        [Required] public string UserId { get; set; } = "";
        public ApplicationUser? User { get; set; }

        public int? TodoItemId { get; set; }
        public TodoItem? TodoItem { get; set; }

        [Required] public NotificationType Type { get; set; }

        [Required, StringLength(200)]
        public string Title { get; set; } = "";

        [StringLength(4000)]
        public string? Message { get; set; }

        [StringLength(256)]
        public string? Url { get; set; }

        public bool IsRead { get; set; }
        public bool IsArchived { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? EmailedAtUtc { get; set; }
        public string? Body { get; set; }
    }
}
