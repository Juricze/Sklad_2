namespace Sklad_2.Models
{
    public class PaymentMethodStats
    {
        public string PaymentMethod { get; set; }
        public int Count { get; set; }
        public decimal TotalAmount { get; set; }
        public double Percentage { get; set; }

        public string AmountFormatted => $"{TotalAmount:C}";
        public string PercentageFormatted => $"{Percentage:F1}";
    }
}
