using Sklad_2.Models;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Sklad_2.Services
{
    public interface IGiftCardService
    {
        /// <summary>
        /// Načte dárkový poukaz podle EAN kódu
        /// </summary>
        Task<GiftCard> GetGiftCardByEanAsync(string ean);

        /// <summary>
        /// Načte všechny dárkové poukazy
        /// </summary>
        Task<List<GiftCard>> GetAllGiftCardsAsync();

        /// <summary>
        /// Načte dárkové poukazy podle stavu
        /// </summary>
        Task<List<GiftCard>> GetGiftCardsByStatusAsync(GiftCardStatus status);

        /// <summary>
        /// Přidá nový dárkový poukaz do systému (naskladnění)
        /// </summary>
        Task<(bool Success, string Message)> AddGiftCardAsync(GiftCard giftCard);

        /// <summary>
        /// Validuje, zda lze poukaz prodat (stav NotIssued)
        /// </summary>
        Task<(bool CanSell, string Message)> CanSellGiftCardAsync(string ean);

        /// <summary>
        /// Prodá dárkový poukaz (změní stav NotIssued → Issued)
        /// </summary>
        /// <param name="ean">EAN kód poukazu</param>
        /// <param name="receiptId">ID účtenky, na které byl prodán</param>
        /// <param name="userName">Jméno prodavače</param>
        Task<(bool Success, string Message)> SellGiftCardAsync(string ean, int receiptId, string userName);

        /// <summary>
        /// Validuje, zda lze poukaz uplatnit (stav Issued, neexpirovaný)
        /// </summary>
        Task<(bool CanUse, string Message, GiftCard GiftCard)> CanUseGiftCardAsync(string ean);

        /// <summary>
        /// Uplatní dárkový poukaz jako platbu (změní stav Issued → Used)
        /// </summary>
        /// <param name="ean">EAN kód poukazu</param>
        /// <param name="receiptId">ID účtenky, na které byl využit</param>
        /// <param name="userName">Jméno prodavače</param>
        Task<(bool Success, string Message)> UseGiftCardAsync(string ean, int receiptId, string userName);

        /// <summary>
        /// Stornuje prodej poukazu (změní stav Issued → NotIssued)
        /// </summary>
        Task<(bool Success, string Message)> CancelSaleAsync(string ean);

        /// <summary>
        /// Stornuje uplatnění poukazu (změní stav Used → Issued)
        /// </summary>
        Task<(bool Success, string Message)> CancelRedemptionAsync(string ean);

        /// <summary>
        /// Označí poukaz jako zrušený (pro vadné/ztracené poukazy)
        /// </summary>
        Task<(bool Success, string Message)> MarkAsCancelledAsync(string ean, string reason);

        /// <summary>
        /// Aktualizuje expirační datum poukazu
        /// </summary>
        Task<(bool Success, string Message)> SetExpirationDateAsync(string ean, DateTime? expirationDate);
    }
}
