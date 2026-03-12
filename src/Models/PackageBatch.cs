using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
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

        public string OwnerUserId { get; set; } = null!;
        public UserCache OwnerUser { get; set; } = null!;

        public DateTimeOffset RoastedAt { get; set; } = DateTimeOffset.UtcNow;

        [Range(1, 30)]
        public int BatchCount { get; set; }

        [Range(1, 30)]
        public int TotalCount { get; set; }

        [Range(0, 30)]
        public int RemainingCount { get; set; }

        public ICollection<BatchTake> Takes { get; } = [];
    }
}
