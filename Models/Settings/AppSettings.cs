using System;
using System.Collections.Generic;
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
        public string ScannerPath { get; set; }
        public string BackupPath { get; set; } = null; // Explicitly null - must be set by user
        public bool AllowManualDiscounts { get; set; }
        public DateTime? LastSaleLoginDate { get; set; }
        public DateTime? LastDayCloseDate { get; set; }

        // Product categories (dynamically managed)
        public List<string> Categories { get; set; } = new List<string>
        {
            "Potraviny",
            "Drogerie",
            "Elektronika",
            "Ostatní"
        };
    }
}