using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.ComponentModel.DataAnnotations;

namespace Sklad_2.Models
{
    /// <summary>
    /// Dárkový poukaz - eviduje celý životní cyklus poukazu
    /// od naskladnění přes prodej až po využití
    /// </summary>
    public partial class GiftCard : ObservableObject
    {
        [Key]
        [ObservableProperty]
        private int id;

        /// <summary>
        /// Unikátní EAN kód z fyzického poukazu
        /// </summary>
        [ObservableProperty]
        private string ean = string.Empty;

        /// <summary>
        /// Nominální hodnota poukazu (500 Kč, 1000 Kč, atd.)
        /// </summary>
        [ObservableProperty]
        private decimal value;

        /// <summary>
        /// Aktuální stav poukazu
        /// </summary>
        [ObservableProperty]
        private GiftCardStatus status = GiftCardStatus.NotIssued;

        /// <summary>
        /// Poznámky k poukazu
        /// </summary>
        [ObservableProperty]
        private string notes = string.Empty;

        // === VYDÁNÍ (Prodej poukazu) ===

        /// <summary>
        /// Datum a čas kdy byl poukaz prodán zákazníkovi
        /// </summary>
        [ObservableProperty]
        private DateTime? issuedDate;

        /// <summary>
        /// ID účtenky na které byl poukaz prodán
        /// </summary>
        [ObservableProperty]
        private int? issuedOnReceiptId;

        /// <summary>
        /// Kdo poukaz prodal
        /// </summary>
        [ObservableProperty]
        private string issuedByUser = string.Empty;

        // === VYUŽITÍ (Uplatnění poukazu) ===

        /// <summary>
        /// Datum a čas kdy byl poukaz využit
        /// </summary>
        [ObservableProperty]
        private DateTime? usedDate;

        /// <summary>
        /// ID účtenky na které byl poukaz využit jako platba
        /// </summary>
        [ObservableProperty]
        private int? usedOnReceiptId;

        /// <summary>
        /// Kdo zpracoval využití poukazu
        /// </summary>
        [ObservableProperty]
        private string usedByUser = string.Empty;

        // === EXPIRACE ===

        /// <summary>
        /// Datum platnosti poukazu (null = bez expirace)
        /// </summary>
        [ObservableProperty]
        private DateTime? expirationDate;

        /// <summary>
        /// Je poukaz expirovaný?
        /// </summary>
        public bool IsExpired => ExpirationDate.HasValue && DateTime.Now > ExpirationDate.Value;

        // === STORNO ===

        /// <summary>
        /// Je poukaz zrušený (stornovaný)?
        /// </summary>
        [ObservableProperty]
        private bool isCancelled;

        /// <summary>
        /// Datum zrušení
        /// </summary>
        [ObservableProperty]
        private DateTime? cancelledDate;

        /// <summary>
        /// Důvod zrušení
        /// </summary>
        [ObservableProperty]
        private string cancelReason = string.Empty;

        // === COMPUTED PROPERTIES PRO UI ===

        public string ValueFormatted => $"{Value:C}";

        public string StatusFormatted => Status switch
        {
            GiftCardStatus.NotIssued => "Neprodaný",
            GiftCardStatus.Issued => "Prodaný nevyužitý",
            GiftCardStatus.Used => "Využitý",
            GiftCardStatus.Expired => "Expirovaný",
            GiftCardStatus.Cancelled => "Zrušený",
            _ => Status.ToString()
        };

        public string IssuedDateFormatted => IssuedDate?.ToString("dd.MM.yyyy HH:mm") ?? "-";
        public string UsedDateFormatted => UsedDate?.ToString("dd.MM.yyyy HH:mm") ?? "-";
        public string ExpirationDateFormatted => ExpirationDate?.ToString("dd.MM.yyyy") ?? "Bez omezení";

        /// <summary>
        /// Barva pro zobrazení stavu (zelená/žlutá/červená)
        /// </summary>
        public string StatusColor => Status switch
        {
            GiftCardStatus.NotIssued => "#999999", // Šedá
            GiftCardStatus.Issued => "#FF9500",    // Oranžová (závazek)
            GiftCardStatus.Used => "#34C759",      // Zelená
            GiftCardStatus.Expired => "#FF3B30",   // Červená
            GiftCardStatus.Cancelled => "#FF3B30", // Červená
            _ => "#999999"
        };

        /// <summary>
        /// Ikona pro zobrazení stavu
        /// </summary>
        public string StatusIcon => Status switch
        {
            GiftCardStatus.NotIssued => "\uE7B8",   // Package
            GiftCardStatus.Issued => "\uE7BA",      // Warning
            GiftCardStatus.Used => "\uE73E",        // CheckMark
            GiftCardStatus.Expired => "\uE917",     // Clock
            GiftCardStatus.Cancelled => "\uE711",   // Cancel
            _ => "\uE7B8"
        };

        /// <summary>
        /// Lze poukaz využít? (musí být Issued a ne expirovaný)
        /// </summary>
        public bool CanBeUsed => Status == GiftCardStatus.Issued && !IsExpired;

        /// <summary>
        /// Lze poukaz prodat? (musí být NotIssued)
        /// </summary>
        public bool CanBeSold => Status == GiftCardStatus.NotIssued;
    }
}
