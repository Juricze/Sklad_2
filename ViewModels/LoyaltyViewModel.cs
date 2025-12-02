using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.EntityFrameworkCore;
using Sklad_2.Data;
using Sklad_2.Models;
using Sklad_2.Services;
using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;

namespace Sklad_2.ViewModels
{
    public partial class LoyaltyViewModel : ObservableObject
    {
        private readonly IDbContextFactory<DatabaseContext> _contextFactory;
        private readonly IAuthService _authService;

        public LoyaltyViewModel(IDbContextFactory<DatabaseContext> contextFactory, IAuthService authService)
        {
            _contextFactory = contextFactory;
            _authService = authService;
        }

        // Kolekce členů
        [ObservableProperty]
        private ObservableCollection<LoyaltyCustomer> customers = new();

        [ObservableProperty]
        private LoyaltyCustomer selectedCustomer;

        // Vyhledávání
        [ObservableProperty]
        private string searchText = string.Empty;

        // Nový člen - pole formuláře
        [ObservableProperty]
        private string newFirstName = string.Empty;

        [ObservableProperty]
        private string newLastName = string.Empty;

        [ObservableProperty]
        private string newEmail = string.Empty;

        [ObservableProperty]
        private string newPhoneNumber = string.Empty;

        [ObservableProperty]
        private string newCardEan = string.Empty;

        // Editace slevy (pouze admin)
        [ObservableProperty]
        private string editDiscountPercent = "0";

        // Status zpráva
        [ObservableProperty]
        private string statusMessage = string.Empty;

        [ObservableProperty]
        private bool isError = false;

        // Statistiky
        public int TotalCount => Customers.Count;
        public decimal TotalPurchasesSum => Customers.Sum(c => c.TotalPurchases);
        public string TotalPurchasesSumFormatted => $"{TotalPurchasesSum:N2} Kč";
        public int WithDiscountCount => Customers.Count(c => c.DiscountPercent > 0);

        // Admin kontrola
        public bool IsAdmin => _authService.CurrentUser?.Role == "Admin";

        partial void OnSearchTextChanged(string value)
        {
            _ = LoadCustomersAsync();
        }

        [RelayCommand]
        public async Task LoadCustomersAsync()
        {
            try
            {
                using var context = await _contextFactory.CreateDbContextAsync();

                var query = context.LoyaltyCustomers.AsNoTracking();

                // Vyhledávání
                if (!string.IsNullOrWhiteSpace(SearchText))
                {
                    var searchLower = SearchText.ToLower();
                    query = query.Where(c =>
                        c.FirstName.ToLower().Contains(searchLower) ||
                        c.LastName.ToLower().Contains(searchLower) ||
                        c.Email.ToLower().Contains(searchLower) ||
                        (c.PhoneNumber != null && c.PhoneNumber.Contains(searchLower)) ||
                        (c.CardEan != null && c.CardEan.Contains(searchLower)));
                }

                var customers = await query
                    .OrderByDescending(c => c.CreatedAt)
                    .ToListAsync();

                Customers.Clear();
                foreach (var customer in customers)
                {
                    Customers.Add(customer);
                }

                OnPropertyChanged(nameof(TotalCount));
                OnPropertyChanged(nameof(TotalPurchasesSum));
                OnPropertyChanged(nameof(TotalPurchasesSumFormatted));
                OnPropertyChanged(nameof(WithDiscountCount));

                Debug.WriteLine($"LoyaltyViewModel: Loaded {customers.Count} customers");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"LoyaltyViewModel: Error loading customers: {ex.Message}");
                SetError($"Chyba při načítání: {ex.Message}");
            }
        }

        [RelayCommand]
        public async Task AddCustomerAsync()
        {
            try
            {
                // Validace
                if (string.IsNullOrWhiteSpace(NewFirstName))
                {
                    SetError("Jméno je povinné.");
                    return;
                }
                if (string.IsNullOrWhiteSpace(NewLastName))
                {
                    SetError("Příjmení je povinné.");
                    return;
                }
                if (string.IsNullOrWhiteSpace(NewPhoneNumber))
                {
                    SetError("Telefon je povinný.");
                    return;
                }

                using var context = await _contextFactory.CreateDbContextAsync();

                // Přidat +420 prefix k telefonu
                var phoneNumber = NewPhoneNumber.Trim();
                phoneNumber = phoneNumber.StartsWith("+420") ? phoneNumber : $"+420{phoneNumber}";

                // Kontrola unikátnosti telefonu (povinný, musí být unikátní)
                var phoneExists = await context.LoyaltyCustomers
                    .AsNoTracking()
                    .AnyAsync(c => c.PhoneNumber == phoneNumber);

                if (phoneExists)
                {
                    SetError("Tento telefon již existuje.");
                    return;
                }

                // Kontrola unikátnosti emailu (pouze pokud je vyplněn)
                if (!string.IsNullOrWhiteSpace(NewEmail))
                {
                    var emailExists = await context.LoyaltyCustomers
                        .AsNoTracking()
                        .AnyAsync(c => c.Email.ToLower() == NewEmail.ToLower());

                    if (emailExists)
                    {
                        SetError("Tento email již existuje.");
                        return;
                    }
                }

                // Kontrola unikátnosti EAN (pokud je vyplněn)
                if (!string.IsNullOrWhiteSpace(NewCardEan))
                {
                    var eanExists = await context.LoyaltyCustomers
                        .AsNoTracking()
                        .AnyAsync(c => c.CardEan == NewCardEan);

                    if (eanExists)
                    {
                        SetError("Tato kartička již existuje.");
                        return;
                    }
                }

                var customer = new LoyaltyCustomer
                {
                    FirstName = NewFirstName.Trim(),
                    LastName = NewLastName.Trim(),
                    Email = string.IsNullOrWhiteSpace(NewEmail) ? string.Empty : NewEmail.Trim(),
                    PhoneNumber = phoneNumber,
                    CardEan = string.IsNullOrWhiteSpace(NewCardEan) ? string.Empty : NewCardEan.Trim(),
                    DiscountPercent = 0,
                    TotalPurchases = 0,
                    CreatedAt = DateTime.Now
                };

                context.LoyaltyCustomers.Add(customer);
                await context.SaveChangesAsync();

                // Vyčistit formulář
                NewFirstName = string.Empty;
                NewLastName = string.Empty;
                NewEmail = string.Empty;
                NewPhoneNumber = string.Empty;
                NewCardEan = string.Empty;

                SetSuccess($"Člen {customer.FullName} byl přidán.");
                await LoadCustomersAsync();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"LoyaltyViewModel: Error adding customer: {ex.Message}");
                SetError($"Chyba při přidávání: {ex.Message}");
            }
        }

        [RelayCommand]
        public async Task DeleteCustomerAsync()
        {
            if (SelectedCustomer == null) return;

            if (!IsAdmin)
            {
                SetError("Pouze admin může mazat členy.");
                return;
            }

            try
            {
                using var context = await _contextFactory.CreateDbContextAsync();

                var customer = await context.LoyaltyCustomers
                    .FirstOrDefaultAsync(c => c.Id == SelectedCustomer.Id);

                if (customer != null)
                {
                    var name = customer.FullName;
                    context.LoyaltyCustomers.Remove(customer);
                    await context.SaveChangesAsync();

                    SetSuccess($"Člen {name} byl smazán.");
                    SelectedCustomer = null;
                    await LoadCustomersAsync();
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"LoyaltyViewModel: Error deleting customer: {ex.Message}");
                SetError($"Chyba při mazání: {ex.Message}");
            }
        }

        [RelayCommand]
        public async Task UpdateDiscountAsync()
        {
            if (SelectedCustomer == null) return;

            if (!IsAdmin)
            {
                SetError("Pouze admin může nastavovat slevy.");
                return;
            }

            if (!decimal.TryParse(EditDiscountPercent, out var discount) || discount < 0 || discount > 30)
            {
                SetError("Sleva musí být číslo mezi 0 a 30.");
                return;
            }

            try
            {
                using var context = await _contextFactory.CreateDbContextAsync();

                var customer = await context.LoyaltyCustomers
                    .FirstOrDefaultAsync(c => c.Id == SelectedCustomer.Id);

                if (customer != null)
                {
                    customer.DiscountPercent = discount;
                    await context.SaveChangesAsync();

                    SetSuccess($"Sleva pro {customer.FullName} nastavena na {discount}%.");
                    await LoadCustomersAsync();
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"LoyaltyViewModel: Error updating discount: {ex.Message}");
                SetError($"Chyba při nastavení slevy: {ex.Message}");
            }
        }

        [RelayCommand]
        public async Task UpdateCustomerAsync(LoyaltyCustomer updatedCustomer)
        {
            if (updatedCustomer == null) return;

            try
            {
                using var context = await _contextFactory.CreateDbContextAsync();

                var customer = await context.LoyaltyCustomers
                    .FirstOrDefaultAsync(c => c.Id == updatedCustomer.Id);

                if (customer != null)
                {
                    // Validace povinných polí
                    if (string.IsNullOrWhiteSpace(updatedCustomer.FirstName))
                    {
                        SetError("Jméno je povinné.");
                        return;
                    }
                    if (string.IsNullOrWhiteSpace(updatedCustomer.LastName))
                    {
                        SetError("Příjmení je povinné.");
                        return;
                    }
                    if (string.IsNullOrWhiteSpace(updatedCustomer.PhoneNumber))
                    {
                        SetError("Telefon je povinný.");
                        return;
                    }

                    // Kontrola unikátnosti telefonu (pokud se změnil)
                    if (customer.PhoneNumber != updatedCustomer.PhoneNumber)
                    {
                        var phoneExists = await context.LoyaltyCustomers
                            .AsNoTracking()
                            .AnyAsync(c => c.PhoneNumber == updatedCustomer.PhoneNumber && c.Id != updatedCustomer.Id);

                        if (phoneExists)
                        {
                            SetError("Tento telefon již existuje.");
                            return;
                        }
                    }

                    // Kontrola unikátnosti emailu (pokud je vyplněn a změnil se)
                    if (!string.IsNullOrWhiteSpace(updatedCustomer.Email) &&
                        customer.Email.ToLower() != updatedCustomer.Email.ToLower())
                    {
                        var emailExists = await context.LoyaltyCustomers
                            .AsNoTracking()
                            .AnyAsync(c => c.Email.ToLower() == updatedCustomer.Email.ToLower() && c.Id != updatedCustomer.Id);

                        if (emailExists)
                        {
                            SetError("Tento email již existuje.");
                            return;
                        }
                    }

                    // Kontrola unikátnosti EAN (pokud se změnil)
                    if (!string.IsNullOrWhiteSpace(updatedCustomer.CardEan) &&
                        customer.CardEan != updatedCustomer.CardEan)
                    {
                        var eanExists = await context.LoyaltyCustomers
                            .AsNoTracking()
                            .AnyAsync(c => c.CardEan == updatedCustomer.CardEan && c.Id != updatedCustomer.Id);

                        if (eanExists)
                        {
                            SetError("Tato kartička již existuje.");
                            return;
                        }
                    }

                    customer.FirstName = updatedCustomer.FirstName;
                    customer.LastName = updatedCustomer.LastName;
                    customer.Email = string.IsNullOrWhiteSpace(updatedCustomer.Email) ? string.Empty : updatedCustomer.Email;
                    customer.PhoneNumber = string.IsNullOrWhiteSpace(updatedCustomer.PhoneNumber) ? string.Empty : updatedCustomer.PhoneNumber;
                    customer.CardEan = string.IsNullOrWhiteSpace(updatedCustomer.CardEan) ? string.Empty : updatedCustomer.CardEan;
                    customer.DiscountPercent = updatedCustomer.DiscountPercent;

                    await context.SaveChangesAsync();

                    SetSuccess($"Údaje člena {customer.FullName} byly aktualizovány.");
                    await LoadCustomersAsync();
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"LoyaltyViewModel: Error updating customer: {ex.Message}");
                SetError($"Chyba při aktualizaci: {ex.Message}");
            }
        }

        private void SetError(string message)
        {
            StatusMessage = message;
            IsError = true;
        }

        private void SetSuccess(string message)
        {
            StatusMessage = message;
            IsError = false;
        }

        public void ClearStatus()
        {
            StatusMessage = string.Empty;
            IsError = false;
        }
    }
}
