using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace Sklad_2.Models
{
    public class Receipt
    {
        [Key]
        public int ReceiptId { get; set; }
        public DateTime SaleDate { get; set; }
        public decimal TotalAmount { get; set; } // Total with VAT
        public string PaymentMethod { get; set; }

        // Seller info at the time of sale
        public string ShopName { get; set; }
        public string ShopAddress { get; set; }
        public string CompanyId { get; set; }
        public string VatId { get; set; }
        public bool IsVatPayer { get; set; }

        // VAT info
        public decimal TotalAmountWithoutVat { get; set; }
        public decimal TotalVatAmount { get; set; }

        // Formatted properties
        public string TotalAmountFormatted => $"{TotalAmount:C}";
        public string SaleDateFormatted => $"{SaleDate.Date:d}";
        public string TotalAmountWithoutVatFormatted => $"{TotalAmountWithoutVat:C}";
        public string TotalVatAmountFormatted => $"{TotalVatAmount:C}";

        public ICollection<ReceiptItem> Items { get; set; }
    }
}
