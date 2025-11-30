using CommunityToolkit.Mvvm.ComponentModel;
using System.ComponentModel.DataAnnotations;

namespace Sklad_2.Models
{
    /// <summary>
    /// Výrobce/Značka produktu (např. Maybelline, L'Oréal, Essence...)
    /// </summary>
    public partial class Brand : ObservableObject
    {
        [Key]
        [ObservableProperty]
        private int id;

        /// <summary>
        /// Název značky
        /// </summary>
        [ObservableProperty]
        private string name = string.Empty;

        /// <summary>
        /// Poznámky k značce (volitelné)
        /// </summary>
        [ObservableProperty]
        private string description = string.Empty;
    }
}
