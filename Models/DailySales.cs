using System;

namespace Sklad_2.Models
{
    public class DailySales
    {
        public DateTime Date { get; set; }
        public decimal TotalAmount { get; set; }
        public int NumberOfSales { get; set; }

        public string DateLabel => Date.ToString("dd.MM");
        public string ShortDateLabel => Date.ToString("dd");
    }
}
