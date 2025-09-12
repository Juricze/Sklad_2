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
        public decimal TotalAmount { get; set; }
        public string PaymentMethod { get; set; } // e.g., "Hotově", "Karta"

        public string TotalAmountFormatted => $"{TotalAmount:C}";
        public string SaleDateFormatted => $"{SaleDate.Date:d}";

        public ICollection<ReceiptItem> Items { get; set; }
    }
}