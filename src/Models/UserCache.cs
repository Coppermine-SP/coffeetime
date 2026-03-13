using System.ComponentModel.DataAnnotations;

namespace coffeetime.Models
{
    public class UserCache
    {
        [Key]
        [MaxLength(36)]
        public required string UserObjectGuid { get; set; }

        [Required]
        [MaxLength(30)]
        public required string UserDisplayName { get; set; }

        public ICollection<BatchTake> TakenBatches { get; } = [];
        public ICollection<PackageBatch> OwnedBatches { get; } = [];
    }
}
