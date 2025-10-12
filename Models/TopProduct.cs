namespace Sklad_2.Models
{
    public class TopProduct
    {
        public string ProductName { get; set; }
        public int QuantitySold { get; set; }
        public decimal TotalRevenue { get; set; }

        public string RevenueFormatted => $"{TotalRevenue:C}";
        public double PercentageOfTotal { get; set; }
    }
}
