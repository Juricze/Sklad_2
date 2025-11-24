namespace Sklad_2.Models
{
    /// <summary>
    /// Stavy dárkového poukazu během jeho životního cyklu
    /// </summary>
    public enum GiftCardStatus
    {
        /// <summary>
        /// Naskladněný, neprodaný - lze prodat
        /// </summary>
        NotIssued,

        /// <summary>
        /// Prodaný, nevyužitý - lze uplatnit
        /// </summary>
        Issued,

        /// <summary>
        /// Využitý - již nelze použít
        /// </summary>
        Used,

        /// <summary>
        /// Expirovaný - nelze použít
        /// </summary>
        Expired,

        /// <summary>
        /// Zrušený (storno) - nelze použít
        /// </summary>
        Cancelled
    }
}
