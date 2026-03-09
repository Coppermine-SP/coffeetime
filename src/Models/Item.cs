using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace coffeetime.Models
{
    public class Item
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int ItemId { get; set; }

        [Required]
        public required string ItemName { get; set; }

        [Required]
        public string ItemDescription { get; set; } = string.Empty;

        [Required]
        public uint ItemPrice { get; set; }

        public virtual List<PackageBatch> Batches { get; set; } = [];
    }
}
