using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Sklad_2.Models
{
    /// <summary>
    /// Junction table pro many-to-many vztah mezi Receipt a GiftCard.
    /// Umožňuje uplatnění více dárkových poukazů na jedné účtence.
    /// </summary>
    public class ReceiptGiftCardRedemption
    {
        [Key]
        public int Id { get; set; }

        /// <summary>
        /// ID účtenky, na které byl poukaz uplatněn
        /// </summary>
        [Required]
        public int ReceiptId { get; set; }

        /// <summary>
        /// EAN kód dárkového poukazu, který byl uplatněn
        /// </summary>
        [Required]
        public string GiftCardEan { get; set; } = string.Empty;

        /// <summary>
        /// Částka, která byla z tohoto poukazu uplatněna (může být nižší než hodnota poukazu)
        /// </summary>
        [Required]
        [Column(TypeName = "decimal(18,2)")]
        public decimal RedeemedAmount { get; set; }

        // Navigation properties
        [ForeignKey(nameof(ReceiptId))]
        public Receipt Receipt { get; set; }

        [ForeignKey(nameof(GiftCardEan))]
        public GiftCard GiftCard { get; set; }
    }
}
