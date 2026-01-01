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
        public string SecondaryBackupPath { get; set; } = null; // Optional secondary backup (e.g., OneDrive)
        public bool AllowManualDiscounts { get; set; }

        /// <summary>
        /// Datum posledního ZAHÁJENÉHO obchodního dne (kdy uživatel klikl "Ano, zahájit nový den").
        /// KRITICKÉ: Toto pole se NESMÍ měnit při zavření aplikace!
        /// Měnit POUZE:
        /// - MainWindow.OnFirstActivated (po potvrzení zahájení dne)
        /// - TrzbyUzavirkPage (po uzavření + nabídka nového dne)
        /// - MainWindow backup (pouze při time shift recovery - po double confirmation)
        /// </summary>
        public DateTime? LastSaleLoginDate { get; set; }

        /// <summary>
        /// Datum poslední PROVEDENÉ denní uzavírky.
        /// KRITICKÉ: Toto pole se NESMÍ měnit ručně!
        /// Měnit POUZE:
        /// - DailyCloseService.CloseDayAsync (při úspěšné uzavírce)
        /// - MainWindow backup (pouze při time shift recovery - sync s DB po double confirmation)
        /// </summary>
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