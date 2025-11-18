using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Sklad_2.Models;
using Sklad_2.ViewModels;
using Sklad_2.Views.Dialogs;
using System;
using System.Threading.Tasks;

namespace Sklad_2.Views
{
    public sealed partial class NastaveniPage : Page
    {
        public NastaveniViewModel ViewModel { get; }
        public CategoryManagementViewModel CategoryVM { get; }
        public UserManagementViewModel UserMgmtVM { get; }

        public NastaveniPage()
        {
            // IMPORTANT: ViewModels must be set BEFORE InitializeComponent() for x:Bind to work properly
            ViewModel = ((App)Application.Current).Services.GetRequiredService<NastaveniViewModel>();
            CategoryVM = ((App)Application.Current).Services.GetRequiredService<CategoryManagementViewModel>();
            UserMgmtVM = ((App)Application.Current).Services.GetRequiredService<UserManagementViewModel>();

            this.InitializeComponent();

            // Connect UserMgmtVM dialog handlers
            UserMgmtVM.RequestAddUserAsync += HandleRequestAddUserAsync;
            UserMgmtVM.RequestEditUserAsync += HandleRequestEditUserAsync;

            // Pre-fill the sale password box with current password (if set)
            if (!string.IsNullOrEmpty(ViewModel.Settings.SalePassword))
            {
                NewSalePasswordBox.Password = ViewModel.Settings.SalePassword;
            }
        }

        private void NewAdminPasswordBox_PasswordChanged(object sender, RoutedEventArgs e)
        {
            ViewModel.NewAdminPassword = NewAdminPasswordBox.Password;
        }

        private void ConfirmAdminPasswordBox_PasswordChanged(object sender, RoutedEventArgs e)
        {
            ViewModel.ConfirmAdminPassword = ConfirmAdminPasswordBox.Password;
        }

        private void NewSalePasswordBox_PasswordChanged(object sender, RoutedEventArgs e)
        {
            ViewModel.NewSalePassword = NewSalePasswordBox.Password;
        }

        private async void NavView_ItemInvoked(NavigationView sender, NavigationViewItemInvokedEventArgs args)
        {
            if (args.InvokedItemContainer != null)
            {
                var tag = args.InvokedItemContainer.Tag.ToString();
                CompanySettingsPanel.Visibility = tag == "Company" ? Visibility.Visible : Visibility.Collapsed;
                VatSettingsPanel.Visibility = tag == "VAT" ? Visibility.Visible : Visibility.Collapsed;
                CategoriesPanel.Visibility = tag == "Categories" ? Visibility.Visible : Visibility.Collapsed;
                UsersPanel.Visibility = tag == "Users" ? Visibility.Visible : Visibility.Collapsed;
                PasswordSettingsPanel.Visibility = tag == "Passwords" ? Visibility.Visible : Visibility.Collapsed;
                SystemSettingsPanel.Visibility = tag == "System" ? Visibility.Visible : Visibility.Collapsed;
                AboutPanel.Visibility = tag == "About" ? Visibility.Visible : Visibility.Collapsed;

                // Load users when Users panel is shown
                if (tag == "Users")
                {
                    await UserMgmtVM.LoadUsersAsync();
                }
            }
        }

        private async Task<(bool confirmed, string username, string displayName, string password, string role)> HandleRequestAddUserAsync()
        {
            var dialog = new AddEditUserDialog();
            dialog.XamlRoot = this.XamlRoot;

            var result = await dialog.ShowAsync();

            if (result == ContentDialogResult.Primary)
            {
                return (true, dialog.Username, dialog.DisplayName, dialog.Password, dialog.Role);
            }

            return (false, null, null, null, null);
        }

        private async Task<(bool confirmed, string username, string displayName, string password, string role)> HandleRequestEditUserAsync(User user)
        {
            var dialog = new AddEditUserDialog();
            dialog.SetEditMode(user);
            dialog.XamlRoot = this.XamlRoot;

            var result = await dialog.ShowAsync();

            if (result == ContentDialogResult.Primary)
            {
                return (true, dialog.Username, dialog.DisplayName, dialog.Password, dialog.Role);
            }

            return (false, null, null, null, null);
        }
    }
}
