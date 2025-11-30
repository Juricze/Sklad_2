using CommunityToolkit.Mvvm.ComponentModel;
using System.ComponentModel.DataAnnotations;

namespace Sklad_2.Models
{
    /// <summary>
    /// Kategorie produktu (např. Pudry, Makeupy, Štětce...)
    /// Plochá struktura bez hierarchie.
    /// </summary>
    public partial class ProductCategory : ObservableObject
    {
        [Key]
        [ObservableProperty]
        private int id;

        /// <summary>
        /// Název kategorie
        /// </summary>
        [ObservableProperty]
        private string name = string.Empty;

        /// <summary>
        /// Poznámky k kategorii (volitelné)
        /// </summary>
        [ObservableProperty]
        private string description = string.Empty;
    }
}
