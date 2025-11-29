using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.ComponentModel.DataAnnotations;

namespace Sklad_2.Models
{
    /// <summary>
    /// Člen věrnostního programu
    /// </summary>
    public partial class LoyaltyCustomer : ObservableObject
    {
        [Key]
        public int Id { get; set; }

        [ObservableProperty]
        private string firstName = string.Empty;

        [ObservableProperty]
        private string lastName = string.Empty;

        [ObservableProperty]
        private string email = string.Empty;

        [ObservableProperty]
        private string cardEan = string.Empty;

        [ObservableProperty]
        private decimal discountPercent;

        [ObservableProperty]
        private decimal totalPurchases;

        [ObservableProperty]
        private DateTime createdAt;

        // Computed properties pro UI
        public string FullName => $"{FirstName} {LastName}";
        public string DiscountFormatted => DiscountPercent > 0 ? $"{DiscountPercent:N0} %" : "-";
        public string TotalPurchasesFormatted => $"{TotalPurchases:N2} Kč";
        public string CardEanFormatted => string.IsNullOrEmpty(CardEan) ? "Bez kartičky" : CardEan;
        public string CreatedAtFormatted => CreatedAt.ToString("dd.MM.yyyy");

        /// <summary>
        /// Zamaskovaný email pro účtenku: pavel@********
        /// </summary>
        public string MaskedEmail
        {
            get
            {
                if (string.IsNullOrEmpty(Email)) return string.Empty;
                var parts = Email.Split('@');
                if (parts.Length != 2) return Email;
                var localPart = parts[0];
                // Zobrazit první část před @ a zbytek nahradit hvězdičkami
                return $"{localPart}@********";
            }
        }

        // Pro vyhledávání
        public string SearchText => $"{FirstName} {LastName} {Email} {CardEan}".ToLower();
    }
}
