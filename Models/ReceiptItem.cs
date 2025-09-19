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

        public string ProductEan { get; set; }
        public string ProductName { get; set; }
        public int Quantity { get; set; }
        public decimal UnitPrice { get; set; } // Price with VAT
        public decimal TotalPrice { get; set; } // Price with VAT

        // New properties for VAT
        public decimal VatRate { get; set; }
        public decimal PriceWithoutVat { get; set; }
        public decimal VatAmount { get; set; }

        // Formatted properties
        public string UnitPriceFormatted => $"{UnitPrice:C}";
        public string TotalPriceFormatted => $"{TotalPrice:C}";
        public string PriceWithoutVatFormatted => $"{PriceWithoutVat:C}";
        public string VatAmountFormatted => $"{VatAmount:C}";
        public string VatRateFormatted => $"{VatRate:P0}";
    }
}
