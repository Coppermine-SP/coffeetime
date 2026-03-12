using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace coffeetime.Models
{
    public class BatchTake
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int BatchTakeId { get; set; }

        public int BatchId { get; set; }
        public PackageBatch Batch { get; set; } = null!;

        public string TakenByUserId { get; set; } = null!;
        public UserCache TakenByUser { get; set; } = null!;

        [Range(1, 10)]
        public int Quantity { get; set; }

        public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    }
}
