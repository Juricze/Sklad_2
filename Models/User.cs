using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.ComponentModel.DataAnnotations;

namespace Sklad_2.Models
{
    public partial class User : ObservableObject
    {
        [Key]
        [ObservableProperty]
        private int userId;

        /// <summary>
        /// Unique username for login (e.g., "admin", "petr.novak")
        /// </summary>
        [ObservableProperty]
        private string username;

        /// <summary>
        /// Display name shown in UI and on receipts (e.g., "Administrátor", "Petr Novák")
        /// </summary>
        [ObservableProperty]
        private string displayName;

        /// <summary>
        /// Hashed password (plain text for now, can be hashed later if needed)
        /// </summary>
        [ObservableProperty]
        private string password;

        /// <summary>
        /// User role: "Admin" or "Cashier"
        /// </summary>
        [ObservableProperty]
        private string role;

        /// <summary>
        /// Whether user account is active (can login)
        /// </summary>
        [ObservableProperty]
        private bool isActive = true;

        /// <summary>
        /// When the user account was created
        /// </summary>
        [ObservableProperty]
        private DateTime createdDate = DateTime.Now;

        // Computed properties
        public string RoleDisplayName => Role == "Admin" ? "Administrátor" : "Prodavač";
        public string StatusDisplayName => IsActive ? "Aktivní" : "Neaktivní";
    }
}
