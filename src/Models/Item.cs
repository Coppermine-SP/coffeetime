using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace coffeetime.Models
{
    public class Item
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int ItemId { get; set; }

        [MaxLength(50)]
        public required string ItemName { get; set; }

        [MaxLength(200)]
        public string ItemDescription { get; set; } = string.Empty;

        public uint ItemPrice { get; set; }

        public virtual List<PackageBatch> Batches { get; set; } = [];
    }
}
