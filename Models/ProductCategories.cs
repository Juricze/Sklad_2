using System.Collections.Generic;

namespace Sklad_2.Models
{
    public static class ProductCategories
    {
        public static List<string> All { get; } = new List<string>
        {
            "Potraviny",
            "Drogerie",
            "Elektronika",
            "Ostatní"
        };
    }
}
