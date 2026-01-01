namespace Sklad_2.Models
{
    /// <summary>
    /// Typy období pro export uzavírek
    /// </summary>
    public enum ExportPeriodType
    {
        /// <summary>
        /// Týdenní export (pondělí-neděle, ISO 8601)
        /// </summary>
        Weekly,

        /// <summary>
        /// Měsíční export
        /// </summary>
        Monthly,

        /// <summary>
        /// Čtvrtletní export (Q1-Q4)
        /// </summary>
        Quarterly,

        /// <summary>
        /// Půlroční export (H1-H2)
        /// </summary>
        HalfYearly,

        /// <summary>
        /// Roční export
        /// </summary>
        Yearly
    }
}
