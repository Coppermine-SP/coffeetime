using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;

namespace coffeetime.Models
{
    public class PackageBatch
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int BatchId { get; set; }

        public int ItemId { get; set; }
        public Item Item { get; set; } = null!;

        [MaxLength(36)]
        public string OwnerUserId { get; set; } = null!;
        public UserCache OwnerUser { get; set; } = null!;

        [Required(ErrorMessage = "로스팅일자는 필수 항목입니다.")]
        public DateTimeOffset RoastedAtUtc { get; set; } = DateTimeOffset.UtcNow;

        [Range(1, 100, ErrorMessage = "배치 수량은 1에서 100사이의 숫자여야 합니다.")]
        [Required(ErrorMessage = "배치 수량은 필수 항목입니다.")]
        public int BatchCount { get; set; }

        [Range(0, 100)]
        public int RemainingCount { get; set; }

        public ICollection<BatchTake> Takes { get; } = [];
    }
}
