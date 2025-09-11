using System.ComponentModel.DataAnnotations;

namespace Sklad_2.Models
{
    public class Product
    {
        [Key]
        public string Ean { get; set; }

        public string Name { get; set; }

        public string Category { get; set; }

        public decimal SalePrice { get; set; }

        public decimal PurchasePrice { get; set; }

        public decimal VatRate { get; set; }

        public int StockQuantity { get; set; }

        public string SalePriceFormatted => $"{SalePrice:C}";
    }
}
