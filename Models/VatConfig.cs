using System.ComponentModel.DataAnnotations;

namespace Sklad_2.Models
{
    public class VatConfig
    {
        [Key]
        public string CategoryName { get; set; }

        public double Rate { get; set; }
    }
}
