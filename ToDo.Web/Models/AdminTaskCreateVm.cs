using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;

namespace ToDo.Web.Models
{
    public class AdminTaskCreateVm
    {
        [Required, StringLength(200)]
        public string Title { get; set; } = "";

        [Required]
        public string Description { get; set; } = "";

        [Required]
        public string OwnerId { get; set; } = "";

        [Required]
        public DateTime DueAtUtc { get; set; }

        [Range(0, 100)]
        public int PriorityScore { get; set; } = 50;

        // Çoklu dosya
        public List<IFormFile> Attachments { get; set; } = new();
        public List<string>? AttachmentTitles { get; set; } // dosya isimleri (sıralı)

    }
}
