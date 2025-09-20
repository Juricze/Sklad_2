using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace Sklad_2.Models
{
    public class Return
    {
        [Key]
        public int ReturnId { get; set; }
        public DateTime ReturnDate { get; set; }

        // Reference to the original receipt
        public int OriginalReceiptId { get; set; }

        // Seller info at the time of return
        public string ShopName { get; set; }
        public string ShopAddress { get; set; }
        public string CompanyId { get; set; }
        public string VatId { get; set; }
        public bool IsVatPayer { get; set; }

        // Refund totals
        public decimal TotalRefundAmount { get; set; }
        public decimal TotalRefundAmountWithoutVat { get; set; }
        public decimal TotalRefundVatAmount { get; set; }

        // Formatted properties
        public string TotalRefundAmountFormatted => $"{TotalRefundAmount:C}";
        public string TotalRefundAmountWithoutVatFormatted => $"{TotalRefundAmountWithoutVat:C}";
        public string TotalRefundVatAmountFormatted => $"{TotalRefundVatAmount:C}";
        public string ReturnDateFormatted => $"{ReturnDate:g}";

        public ICollection<ReturnItem> Items { get; set; }
    }
}
