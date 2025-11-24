using Sklad_2.Models;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Sklad_2.Services
{
    public class GiftCardService : IGiftCardService
    {
        private readonly IDataService _dataService;

        public GiftCardService(IDataService dataService)
        {
            _dataService = dataService;
        }

        public async Task<GiftCard> GetGiftCardByEanAsync(string ean)
        {
            return await _dataService.GetGiftCardByEanAsync(ean);
        }

        public async Task<List<GiftCard>> GetAllGiftCardsAsync()
        {
            return await _dataService.GetAllGiftCardsAsync();
        }

        public async Task<List<GiftCard>> GetGiftCardsByStatusAsync(GiftCardStatus status)
        {
            return await _dataService.GetGiftCardsByStatusAsync(status);
        }

        public async Task<(bool Success, string Message)> AddGiftCardAsync(GiftCard giftCard)
        {
            // Validace: EAN musí být vyplněný
            if (string.IsNullOrWhiteSpace(giftCard.Ean))
            {
                return (false, "EAN kód poukazu nesmí být prázdný.");
            }

            // Validace: Hodnota musí být kladná
            if (giftCard.Value <= 0)
            {
                return (false, "Hodnota poukazu musí být větší než 0 Kč.");
            }

            // Validace: EAN musí být unikátní
            var existing = await _dataService.GetGiftCardByEanAsync(giftCard.Ean);
            if (existing != null)
            {
                return (false, $"Poukaz s EAN {giftCard.Ean} již existuje.");
            }

            // Nastavit výchozí stav
            giftCard.Status = GiftCardStatus.NotIssued;

            await _dataService.AddGiftCardAsync(giftCard);
            return (true, "Dárkový poukaz byl úspěšně přidán.");
        }

        public async Task<(bool CanSell, string Message)> CanSellGiftCardAsync(string ean)
        {
            var giftCard = await _dataService.GetGiftCardByEanAsync(ean);

            if (giftCard == null)
            {
                return (false, $"Poukaz s EAN {ean} nebyl nalezen.");
            }

            if (giftCard.IsCancelled)
            {
                return (false, "Tento poukaz je zrušený a nelze ho prodat.");
            }

            if (giftCard.Status != GiftCardStatus.NotIssued)
            {
                return (false, $"Poukaz již byl prodán ({giftCard.StatusFormatted}).\n\n" +
                    "💡 Hint: Pokud chcete poukaz UPLATNIT (platit poukazem), použijte sekci 'Uplatnit dárkový poukaz' níže, ne pole pro produkty.");
            }

            return (true, "Poukaz lze prodat.");
        }

        public async Task<(bool Success, string Message)> SellGiftCardAsync(string ean, int receiptId, string userName)
        {
            var giftCard = await _dataService.GetGiftCardByEanAsync(ean);

            if (giftCard == null)
            {
                return (false, $"Poukaz s EAN {ean} nebyl nalezen.");
            }

            // Validace pomocí CanSellGiftCardAsync
            var (canSell, validationMessage) = await CanSellGiftCardAsync(ean);
            if (!canSell)
            {
                return (false, validationMessage);
            }

            // Změna stavu: NotIssued → Issued
            giftCard.Status = GiftCardStatus.Issued;
            giftCard.IssuedDate = DateTime.Now;
            giftCard.IssuedOnReceiptId = receiptId;
            giftCard.IssuedByUser = userName;

            await _dataService.UpdateGiftCardAsync(giftCard);
            return (true, $"Poukaz v hodnotě {giftCard.ValueFormatted} byl úspěšně prodán.");
        }

        public async Task<(bool CanUse, string Message, GiftCard GiftCard)> CanUseGiftCardAsync(string ean)
        {
            var giftCard = await _dataService.GetGiftCardByEanAsync(ean);

            if (giftCard == null)
            {
                return (false, $"Poukaz s EAN {ean} nebyl nalezen.", null);
            }

            if (giftCard.IsCancelled)
            {
                return (false, "Tento poukaz je zrušený a nelze ho použít.", null);
            }

            if (giftCard.Status == GiftCardStatus.NotIssued)
            {
                return (false, "Tento poukaz ještě nebyl prodán.", null);
            }

            if (giftCard.Status == GiftCardStatus.Used)
            {
                return (false, "Tento poukaz již byl použit.", null);
            }

            if (giftCard.IsExpired)
            {
                // Automaticky označit jako expirovaný
                giftCard.Status = GiftCardStatus.Expired;
                await _dataService.UpdateGiftCardAsync(giftCard);
                return (false, $"Platnost poukazu vypršela ({giftCard.ExpirationDateFormatted}).", null);
            }

            if (giftCard.Status != GiftCardStatus.Issued)
            {
                return (false, $"Poukaz nelze použít ({giftCard.StatusFormatted}).", null);
            }

            return (true, $"Poukaz v hodnotě {giftCard.ValueFormatted} lze použít.", giftCard);
        }

        public async Task<(bool Success, string Message)> UseGiftCardAsync(string ean, int receiptId, string userName)
        {
            var (canUse, validationMessage, giftCard) = await CanUseGiftCardAsync(ean);

            if (!canUse || giftCard == null)
            {
                return (false, validationMessage);
            }

            // Změna stavu: Issued → Used
            giftCard.Status = GiftCardStatus.Used;
            giftCard.UsedDate = DateTime.Now;
            giftCard.UsedOnReceiptId = receiptId;
            giftCard.UsedByUser = userName;

            await _dataService.UpdateGiftCardAsync(giftCard);
            return (true, $"Poukaz v hodnotě {giftCard.ValueFormatted} byl úspěšně uplatněn.");
        }

        public async Task<(bool Success, string Message)> CancelSaleAsync(string ean)
        {
            var giftCard = await _dataService.GetGiftCardByEanAsync(ean);

            if (giftCard == null)
            {
                return (false, $"Poukaz s EAN {ean} nebyl nalezen.");
            }

            if (giftCard.Status != GiftCardStatus.Issued)
            {
                return (false, $"Nelze stornovat prodej poukazu ve stavu '{giftCard.StatusFormatted}'.");
            }

            // Změna stavu: Issued → NotIssued
            giftCard.Status = GiftCardStatus.NotIssued;
            giftCard.IssuedDate = null;
            giftCard.IssuedOnReceiptId = null;
            giftCard.IssuedByUser = string.Empty;

            await _dataService.UpdateGiftCardAsync(giftCard);
            return (true, "Prodej poukazu byl stornován.");
        }

        public async Task<(bool Success, string Message)> CancelRedemptionAsync(string ean)
        {
            var giftCard = await _dataService.GetGiftCardByEanAsync(ean);

            if (giftCard == null)
            {
                return (false, $"Poukaz s EAN {ean} nebyl nalezen.");
            }

            if (giftCard.Status != GiftCardStatus.Used)
            {
                return (false, $"Nelze stornovat uplatnění poukazu ve stavu '{giftCard.StatusFormatted}'.");
            }

            // Změna stavu: Used → Issued
            giftCard.Status = GiftCardStatus.Issued;
            giftCard.UsedDate = null;
            giftCard.UsedOnReceiptId = null;
            giftCard.UsedByUser = string.Empty;

            await _dataService.UpdateGiftCardAsync(giftCard);
            return (true, "Uplatnění poukazu bylo stornováno.");
        }

        public async Task<(bool Success, string Message)> MarkAsCancelledAsync(string ean, string reason)
        {
            var giftCard = await _dataService.GetGiftCardByEanAsync(ean);

            if (giftCard == null)
            {
                return (false, $"Poukaz s EAN {ean} nebyl nalezen.");
            }

            if (giftCard.Status == GiftCardStatus.Used)
            {
                return (false, "Již využitý poukaz nelze zrušit.");
            }

            giftCard.Status = GiftCardStatus.Cancelled;
            giftCard.IsCancelled = true;
            giftCard.CancelledDate = DateTime.Now;
            giftCard.CancelReason = reason;

            await _dataService.UpdateGiftCardAsync(giftCard);
            return (true, "Poukaz byl označen jako zrušený.");
        }

        public async Task<(bool Success, string Message)> SetExpirationDateAsync(string ean, DateTime? expirationDate)
        {
            var giftCard = await _dataService.GetGiftCardByEanAsync(ean);

            if (giftCard == null)
            {
                return (false, $"Poukaz s EAN {ean} nebyl nalezen.");
            }

            if (expirationDate.HasValue && expirationDate.Value < DateTime.Now)
            {
                return (false, "Datum expirace nemůže být v minulosti.");
            }

            giftCard.ExpirationDate = expirationDate;

            await _dataService.UpdateGiftCardAsync(giftCard);
            return (true, expirationDate.HasValue
                ? $"Datum expirace bylo nastaveno na {expirationDate.Value:dd.MM.yyyy}."
                : "Expirace byla odstraněna.");
        }
    }
}
