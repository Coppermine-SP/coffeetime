using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace coffeetime.Models
{
    public class PackageBatch
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int BatchId { get; set; }

        public required virtual Item Item { get; set; }

        [Required]
        public DateTime RoastedAt { get; set; } = DateTime.Now;

        [Required]
        [Range(1, 30)]
        public required int BatchCount { get; set; }

        public virtual List<Transcation> Transcations { get; set; } = [];
    }
}
