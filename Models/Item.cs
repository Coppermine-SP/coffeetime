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

        public string ItemDescription { get; set; } = string.Empty;

        [Required]
        public int Price { get; set; }

        public virtual List<PackageBatch> Inventories { get; set; } = [];
    }
}
