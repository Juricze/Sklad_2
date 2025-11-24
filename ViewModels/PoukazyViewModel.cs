using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Sklad_2.Models;
using Sklad_2.Services;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;

namespace Sklad_2.ViewModels
{
    public partial class PoukazyViewModel : ObservableObject
    {
        private readonly IGiftCardService _giftCardService;

        // All gift cards (unfiltered) - used for statistics
        private List<GiftCard> _allGiftCards = new List<GiftCard>();

        [ObservableProperty]
        private ObservableCollection<GiftCard> giftCards = new ObservableCollection<GiftCard>();

        [ObservableProperty]
        private GiftCard selectedGiftCard;

        [ObservableProperty]
        private GiftCardStatusFilter selectedStatusFilter = GiftCardStatusFilter.All;

        [ObservableProperty]
        private string searchText = string.Empty;

        // Add new gift card fields
        [ObservableProperty]
        private string newGiftCardEan = string.Empty;

        [ObservableProperty]
        private string newGiftCardValue = "500";

        [ObservableProperty]
        private bool hasExpirationDate = false;

        [ObservableProperty]
        private DateTimeOffset? expirationDate = null;

        [ObservableProperty]
        private string newGiftCardNotes = string.Empty;

        // Error handling
        [ObservableProperty]
        private string lastErrorMessage = string.Empty;

        [ObservableProperty]
        private string lastAddedValue = string.Empty;

        // Filter checkboxes
        [ObservableProperty]
        private bool filterNotIssued = true;

        [ObservableProperty]
        private bool filterIssued = true;

        [ObservableProperty]
        private bool filterUsed = true;

        [ObservableProperty]
        private bool filterExpired = false;

        [ObservableProperty]
        private bool filterCancelled = false;

        // Statistics - always calculated from ALL gift cards (unfiltered)
        public int TotalCount => _allGiftCards.Count;
        public int NotIssuedCount => _allGiftCards.Count(gc => gc.Status == GiftCardStatus.NotIssued);
        public int IssuedCount => _allGiftCards.Count(gc => gc.Status == GiftCardStatus.Issued);
        public int UsedCount => _allGiftCards.Count(gc => gc.Status == GiftCardStatus.Used);
        public int ExpiredCount => _allGiftCards.Count(gc => gc.Status == GiftCardStatus.Expired);
        public int CancelledCount => _allGiftCards.Count(gc => gc.Status == GiftCardStatus.Cancelled);

        public decimal TotalValue => _allGiftCards.Sum(gc => gc.Value);
        public decimal LiabilityAmount => _allGiftCards.Where(gc => gc.Status == GiftCardStatus.Issued).Sum(gc => gc.Value);

        public string TotalValueFormatted => $"{TotalValue:C}";
        public string LiabilityAmountFormatted => $"{LiabilityAmount:C}";

        public List<GiftCardStatusFilterOption> StatusFilterOptions { get; }

        public PoukazyViewModel(IGiftCardService giftCardService)
        {
            _giftCardService = giftCardService;

            StatusFilterOptions = new List<GiftCardStatusFilterOption>
            {
                new GiftCardStatusFilterOption { Name = "Vše", Filter = GiftCardStatusFilter.All },
                new GiftCardStatusFilterOption { Name = "Neprodané", Filter = GiftCardStatusFilter.NotIssued },
                new GiftCardStatusFilterOption { Name = "Prodané (nevyužité)", Filter = GiftCardStatusFilter.Issued },
                new GiftCardStatusFilterOption { Name = "Využité", Filter = GiftCardStatusFilter.Used },
                new GiftCardStatusFilterOption { Name = "Expirované", Filter = GiftCardStatusFilter.Expired },
                new GiftCardStatusFilterOption { Name = "Zrušené", Filter = GiftCardStatusFilter.Cancelled }
            };
        }

        partial void OnSelectedStatusFilterChanged(GiftCardStatusFilter value)
        {
            _ = LoadGiftCardsAsync();
        }

        partial void OnSearchTextChanged(string value)
        {
            _ = LoadGiftCardsAsync();
        }

        partial void OnFilterNotIssuedChanged(bool value)
        {
            UpdateFiltersAndReload();
        }

        partial void OnFilterIssuedChanged(bool value)
        {
            UpdateFiltersAndReload();
        }

        partial void OnFilterUsedChanged(bool value)
        {
            UpdateFiltersAndReload();
        }

        partial void OnFilterExpiredChanged(bool value)
        {
            UpdateFiltersAndReload();
        }

        partial void OnFilterCancelledChanged(bool value)
        {
            UpdateFiltersAndReload();
        }

        private void UpdateFiltersAndReload()
        {
            var filters = new List<GiftCardStatus>();
            if (FilterNotIssued) filters.Add(GiftCardStatus.NotIssued);
            if (FilterIssued) filters.Add(GiftCardStatus.Issued);
            if (FilterUsed) filters.Add(GiftCardStatus.Used);
            if (FilterExpired) filters.Add(GiftCardStatus.Expired);
            if (FilterCancelled) filters.Add(GiftCardStatus.Cancelled);

            _activeFilters = filters;
            _ = LoadGiftCardsAsync();
        }

        private List<GiftCardStatus> _activeFilters = new List<GiftCardStatus>
        {
            GiftCardStatus.NotIssued,
            GiftCardStatus.Issued,
            GiftCardStatus.Used
        };

        private int _currentSortIndex = 0;

        public void SetActiveFilters(List<GiftCardStatus> filters)
        {
            _activeFilters = filters ?? new List<GiftCardStatus>();
        }

        public void ApplySorting(int sortIndex)
        {
            _currentSortIndex = sortIndex;
            var list = GiftCards.ToList();

            var sorted = sortIndex switch
            {
                0 => list.OrderByDescending(g => g.IssuedDate ?? DateTime.MinValue).ToList(), // Datum prodeje ↓
                1 => list.OrderBy(g => g.IssuedDate ?? DateTime.MaxValue).ToList(), // Datum prodeje ↑
                2 => list.OrderByDescending(g => g.UsedDate ?? DateTime.MinValue).ToList(), // Datum využití ↓
                3 => list.OrderBy(g => g.UsedDate ?? DateTime.MaxValue).ToList(), // Datum využití ↑
                4 => list.OrderByDescending(g => g.Value).ToList(), // Hodnota ↓
                5 => list.OrderBy(g => g.Value).ToList(), // Hodnota ↑
                6 => list.OrderBy(g => g.Ean).ToList(), // EAN
                _ => list
            };

            GiftCards.Clear();
            foreach (var card in sorted)
            {
                GiftCards.Add(card);
            }
        }

        [RelayCommand]
        private async Task LoadGiftCardsAsync()
        {
            // Get all gift cards
            var allCards = await _giftCardService.GetAllGiftCardsAsync();

            // Store unfiltered list for statistics
            _allGiftCards = allCards;

            // Apply combined status filters
            var filteredCards = allCards;
            if (_activeFilters.Any())
            {
                filteredCards = filteredCards.Where(gc => _activeFilters.Contains(gc.Status)).ToList();
            }

            // Apply search filter
            if (!string.IsNullOrWhiteSpace(SearchText))
            {
                filteredCards = filteredCards.Where(gc =>
                    gc.Ean.Contains(SearchText, StringComparison.OrdinalIgnoreCase) ||
                    gc.Notes.Contains(SearchText, StringComparison.OrdinalIgnoreCase)
                ).ToList();
            }

            // Apply current sorting
            var sorted = _currentSortIndex switch
            {
                0 => filteredCards.OrderByDescending(g => g.IssuedDate ?? DateTime.MinValue).ToList(),
                1 => filteredCards.OrderBy(g => g.IssuedDate ?? DateTime.MaxValue).ToList(),
                2 => filteredCards.OrderByDescending(g => g.UsedDate ?? DateTime.MinValue).ToList(),
                3 => filteredCards.OrderBy(g => g.UsedDate ?? DateTime.MaxValue).ToList(),
                4 => filteredCards.OrderByDescending(g => g.Value).ToList(),
                5 => filteredCards.OrderBy(g => g.Value).ToList(),
                6 => filteredCards.OrderBy(g => g.Ean).ToList(),
                _ => filteredCards
            };

            GiftCards.Clear();
            foreach (var card in sorted)
            {
                GiftCards.Add(card);
            }

            UpdateStatistics();
        }

        [RelayCommand]
        private async Task AddGiftCardAsync()
        {
            if (string.IsNullOrWhiteSpace(NewGiftCardEan))
            {
                return;
            }

            // Parse value
            if (!decimal.TryParse(NewGiftCardValue, out decimal value) || value <= 0)
            {
                return; // Invalid value
            }

            var newCard = new GiftCard
            {
                Ean = NewGiftCardEan.Trim(),
                Value = value,
                Status = GiftCardStatus.NotIssued,
                Notes = NewGiftCardNotes?.Trim() ?? string.Empty,
                ExpirationDate = HasExpirationDate && ExpirationDate.HasValue
                    ? ExpirationDate.Value.DateTime
                    : (DateTime?)null
            };

            var result = await _giftCardService.AddGiftCardAsync(newCard);

            if (result.Success)
            {
                // Save the added value for success dialog
                LastAddedValue = value.ToString("N0");

                // Clear form (keep the value for next card)
                NewGiftCardEan = string.Empty;
                // NewGiftCardValue - keep the same value for batch adding
                HasExpirationDate = false;
                ExpirationDate = null;
                NewGiftCardNotes = string.Empty;
                LastErrorMessage = string.Empty;

                // Reload list
                await LoadGiftCardsAsync();
            }
            else
            {
                // Set error message for UI to display
                LastErrorMessage = result.Message;
            }
        }

        [RelayCommand]
        private async Task DeleteGiftCardAsync(GiftCard giftCard)
        {
            if (giftCard == null) return;

            // Can only delete NotIssued cards
            if (giftCard.Status != GiftCardStatus.NotIssued)
            {
                return;
            }

            await _giftCardService.GetGiftCardByEanAsync(giftCard.Ean); // Just to ensure it exists
            // Note: Actual deletion would need IDataService.DeleteGiftCardAsync
            // For now, we'll mark as Cancelled instead
            await _giftCardService.MarkAsCancelledAsync(giftCard.Ean, "Smazáno administrátorem");
            await LoadGiftCardsAsync();
        }

        [RelayCommand]
        private async Task MarkAsCancelledAsync(GiftCard giftCard)
        {
            if (giftCard == null) return;

            await _giftCardService.MarkAsCancelledAsync(giftCard.Ean, "Zrušeno administrátorem");
            await LoadGiftCardsAsync();
        }

        private void UpdateStatistics()
        {
            OnPropertyChanged(nameof(TotalCount));
            OnPropertyChanged(nameof(NotIssuedCount));
            OnPropertyChanged(nameof(IssuedCount));
            OnPropertyChanged(nameof(UsedCount));
            OnPropertyChanged(nameof(ExpiredCount));
            OnPropertyChanged(nameof(CancelledCount));
            OnPropertyChanged(nameof(TotalValue));
            OnPropertyChanged(nameof(LiabilityAmount));
            OnPropertyChanged(nameof(TotalValueFormatted));
            OnPropertyChanged(nameof(LiabilityAmountFormatted));
        }
    }

    public enum GiftCardStatusFilter
    {
        All,
        NotIssued,
        Issued,
        Used,
        Expired,
        Cancelled
    }

    public class GiftCardStatusFilterOption
    {
        public string Name { get; set; }
        public GiftCardStatusFilter Filter { get; set; }
    }
}
