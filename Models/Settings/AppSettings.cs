using System;
using System.Text.Json.Serialization;

namespace Sklad_2.Models.Settings
{
    public class AppSettings
    {
        public string ShopName { get; set; }
        public string ShopAddress { get; set; }
        public string CompanyId { get; set; }
        public string VatId { get; set; }
        public bool IsVatPayer { get; set; }
        public string PrinterPath { get; set; }
        public string AdminPassword { get; set; }
        public string SalePassword { get; set; }
        public DateTime? LastSaleLoginDate { get; set; }
        public DateTime? LastDayCloseDate { get; set; }
    }
}