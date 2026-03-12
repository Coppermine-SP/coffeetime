using System.ComponentModel.DataAnnotations;

namespace coffeetime.Models
{
    public class UserCache
    {
        [Key]
        public required string UserObjectGuid { get; set; }

        [Required]
        public required string UserDisplayName { get; set; }

        public virtual List<BatchTake> TakenBatches { get; } = [];
        public virtual List<PackageBatch> OwnedBatches { get; } = [];
    }
}
