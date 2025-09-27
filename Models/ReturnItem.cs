using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Sklad_2.Models
{
    public class ReturnItem
    {
        [Key]
        public int ReturnItemId { get; set; }

        public int ReturnId { get; set; }
        [ForeignKey("ReturnId")]
        public Return Return { get; set; }

        // Info about the returned product
        public string ProductEan { get; set; }
        public string ProductName { get; set; }
        public int ReturnedQuantity { get; set; }

        // Financial info for this item
        public decimal UnitPrice { get; set; } // Price with VAT
        public decimal TotalRefund { get; set; } // Total refund for this line
        public decimal VatRate { get; set; }
        public decimal PriceWithoutVat { get; set; }
        public decimal VatAmount { get; set; } // The VAT amount being refunded for this line

        // Formatted properties
        public string UnitPriceFormatted => $"{UnitPrice:C}";
        public string TotalRefundFormatted => $"{TotalRefund:C}";
        public string VatRateFormatted => $"{VatRate} %";
    }
}
