using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace coffeetime.Models
{
    public class Item
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int ItemId { get; set; }

        [Required(ErrorMessage = "원두 이름은 필수 항목입니다.")]
        [MaxLength(50, ErrorMessage = "원두 이름은 50자보다 짧아야 합니다.")]
        public required string ItemName { get; set; }

        [MaxLength(200, ErrorMessage = "원두 설명은 200자보다 짧아야 합니다.")]
        public string ItemDescription { get; set; } = string.Empty;

        [Required(ErrorMessage ="원두 가격은 필수 항목입니다.")]
        [Range(1, int.MaxValue, ErrorMessage = "원두 가격은 1 이상의 숫자여야 합니다.")]
        public int ItemPrice { get; set; }

        [Required(ErrorMessage = "원두 용량은 필수 항목입니다.")]
        [Range(1, int.MaxValue, ErrorMessage = "원두 용량은 1 이상의 숫자여야 합니다.")]
        public int ItemSize { get; set; }

        public virtual List<PackageBatch> Batches { get; set; } = [];
    }
}
