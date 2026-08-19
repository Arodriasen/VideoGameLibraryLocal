using System;

namespace VideoGameLibrary.Models
{
    public class Game
    {
        public int Id { get; set; }

        public string? Barcode { get; set; }

        public string Title { get; set; } = string.Empty;
        public string Platform { get; set; } = string.Empty;
        public string Publisher { get; set; } = string.Empty;
        public string Genre { get; set; } = string.Empty;
        public string Tags { get; set; } = string.Empty;
        public int? Year { get; set; }
        public string CoverUrl { get; set; } = string.Empty;
        public byte[]? CoverData { get; set; }
        public string Notes { get; set; } = string.Empty;
        public int Rating { get; set; }
        public bool Played { get; set; }
        public bool IsWishlist { get; set; }
        public DateTime AddedDate { get; set; } = DateTime.Now;
        public DateTime? DeletedDate { get; set; }
    }
}
