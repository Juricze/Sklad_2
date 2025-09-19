using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Sklad_2.Models
{
    public class ReceiptItem
    {
        [Key]
        public int ReceiptItemId { get; set; }

        public int ReceiptId { get; set; }
        [ForeignKey("ReceiptId")]
        public Receipt Receipt { get; set; }

        public string ProductEan { get; set; } // Store EAN for historical record
        public string ProductName { get; set; } // Store name for historical record
        public int Quantity { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal TotalPrice { get; set; }

        public string UnitPriceFormatted => $"{UnitPrice:C}";
        public string TotalPriceFormatted => $"{TotalPrice:C}";
    }
}