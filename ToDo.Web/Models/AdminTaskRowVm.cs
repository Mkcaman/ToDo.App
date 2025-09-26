using System;

namespace ToDo.Web.Models
{
    public sealed class AdminTaskRowVm
    {
        public int Id { get; set; }
        public string Title { get; set; } = "";
        public string OwnerName { get; set; } = "";
        public string OwnerEmail { get; set; } = "";

        public DateTime AssignedAtUtc { get; set; }      // TodoItem.CreatedAt
        public DateTime? DueAtUtc { get; set; }          // TodoItem.DueAt

        public bool IsDone { get; set; }
        public bool IsOverdue { get; set; }              // !IsDone && DueAt < now
        public string RemainingText { get; set; } = "";  // “2g 5s 10d” / “Süre doldu” / “—”
        public int Priority { get; set; }

        // Göreve eklenen dosyanın (wwwroot altındaki) URL’si
        public string? AttachmentUrl { get; set; }

        // ---- Görünüm kolaylıkları (hesaplanan alanlar) ----

        // “Ali Veli” yoksa e-posta göster
        public string OwnerDisplay => string.IsNullOrWhiteSpace(OwnerName) ? OwnerEmail : OwnerName;

        // Öncelik metni
        public string PriorityText => Priority switch
        {
            2 => "Yüksek",
            1 => "Normal",
            0 => "Düşük",
            _ => "—"
        };

        // Bootstrap rozet sınıfı (öncelik)
        public string PriorityBadgeClass => Priority switch
        {
            2 => "bg-danger",      // Yüksek
            1 => "bg-primary",     // Normal
            0 => "bg-secondary",   // Düşük
            _ => "bg-light text-dark"
        };

        // Durum metni / rozeti
        public string StatusText => IsDone ? "Tamamlandı" : (IsOverdue ? "Geciken" : "Aktif");
        public string StatusBadgeClass => IsDone ? "bg-success" : (IsOverdue ? "bg-danger" : "bg-warning text-dark");

        // Tarihlerin yerel gösterimi
        public string AssignedAtText => AssignedAtUtc.ToLocalTime().ToString("g");
        public string DueAtText => DueAtUtc?.ToLocalTime().ToString("g") ?? "—";

        // Ek dosya bilgileri
        public bool HasAttachment => !string.IsNullOrWhiteSpace(AttachmentUrl);

        public string? AttachmentFileName
        {
            get
            {
                var url = AttachmentUrl;
                if (string.IsNullOrWhiteSpace(url)) return null;
                var idx = url.LastIndexOf('/');
                return (idx >= 0 && idx + 1 < url.Length) ? url.Substring(idx + 1) : url;
            }
        }
    }
}


