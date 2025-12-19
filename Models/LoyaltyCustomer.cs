#nullable enable
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
        private string? email;

        [ObservableProperty]
        private string? cardEan;

        [ObservableProperty]
        private string phoneNumber = string.Empty;

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
        public string PhoneNumberFormatted => string.IsNullOrEmpty(PhoneNumber) ? "—" : PhoneNumber;
        public string CreatedAtFormatted => CreatedAt.ToString("dd.MM.yyyy");

        /// <summary>
        /// Zamaskovaný email pro účtenku: pav***@***.cz (první 3 znaky + *** + poslední 3 znaky)
        /// </summary>
        public string MaskedEmail
        {
            get
            {
                if (string.IsNullOrEmpty(Email)) return string.Empty;

                var parts = Email.Split('@');
                if (parts.Length != 2) return Email;

                var localPart = parts[0];
                var domainPart = parts[1];

                // První 3 znaky lokální části
                var localPrefix = localPart.Length >= 3 ? localPart.Substring(0, 3) : localPart;

                // Poslední 3 znaky domény (.cz, .com, atd.)
                var domainSuffix = domainPart.Length >= 3 ? domainPart.Substring(domainPart.Length - 3) : domainPart;

                return $"{localPrefix}***@***{domainSuffix}";
            }
        }

        /// <summary>
        /// Zamaskovaný telefon pro účtenku: +420 7396***** (předvolba + první 4 čísla + hvězdičky)
        /// </summary>
        public string MaskedPhone
        {
            get
            {
                if (string.IsNullOrEmpty(PhoneNumber)) return string.Empty;

                // Očekáváme formát: +420739612345
                if (!PhoneNumber.StartsWith("+420")) return PhoneNumber;

                // Odstraníme +420
                var numberPart = PhoneNumber.Substring(4);

                // První 4 čísla + hvězdičky
                if (numberPart.Length >= 4)
                {
                    var prefix = numberPart.Substring(0, 4);
                    var maskedPart = new string('*', numberPart.Length - 4);
                    return $"+420 {prefix}{maskedPart}";
                }

                return PhoneNumber;
            }
        }

        /// <summary>
        /// Zamaskovaný kontakt (email NEBO telefon) - priorita: email > telefon
        /// </summary>
        public string MaskedContact
        {
            get
            {
                // Priorita: Email > Telefon
                if (!string.IsNullOrWhiteSpace(Email))
                {
                    return MaskedEmail;
                }
                else if (!string.IsNullOrWhiteSpace(PhoneNumber))
                {
                    return MaskedPhone;
                }
                return "—";
            }
        }

        // Pro vyhledávání
        public string SearchText => $"{FirstName} {LastName} {Email} {PhoneNumber} {CardEan}".ToLower();
    }
}
