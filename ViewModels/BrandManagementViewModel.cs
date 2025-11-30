using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Sklad_2.Models;
using Sklad_2.Services;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;

namespace Sklad_2.ViewModels
{
    public partial class BrandManagementViewModel : ObservableObject
    {
        private readonly IDataService _dataService;

        [ObservableProperty]
        private ObservableCollection<Brand> brands = new();

        [ObservableProperty]
        private Brand selectedBrand;

        public BrandManagementViewModel(IDataService dataService)
        {
            _dataService = dataService;
        }

        public async Task LoadBrandsAsync()
        {
            var brandsList = await _dataService.GetBrandsAsync();
            Brands.Clear();
            foreach (var brand in brandsList)
            {
                Brands.Add(brand);
            }
        }

        [RelayCommand]
        private async Task AddBrandAsync(Brand brand)
        {
            if (brand == null || string.IsNullOrWhiteSpace(brand.Name))
                return;

            await _dataService.AddBrandAsync(brand);
            await LoadBrandsAsync();
        }

        [RelayCommand]
        private async Task UpdateBrandAsync(Brand brand)
        {
            if (brand == null || string.IsNullOrWhiteSpace(brand.Name))
                return;

            await _dataService.UpdateBrandAsync(brand);
            await LoadBrandsAsync();
        }

        [RelayCommand]
        private async Task DeleteBrandAsync(int brandId)
        {
            var productCount = await _dataService.GetProductCountByBrandIdAsync(brandId);
            if (productCount > 0)
            {
                // Cannot delete - has products
                return;
            }

            await _dataService.DeleteBrandAsync(brandId);
            await LoadBrandsAsync();
        }
    }
}
