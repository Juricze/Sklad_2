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

        private async void BrowseBackupFolderButton_Click(object sender, RoutedEventArgs e)
        {
            var folderPicker = new Windows.Storage.Pickers.FolderPicker();
            folderPicker.SuggestedStartLocation = Windows.Storage.Pickers.PickerLocationId.DocumentsLibrary;
            folderPicker.FileTypeFilter.Add("*");

            // Get the window handle from current app window
            var app = Application.Current as App;
            var hWnd = WinRT.Interop.WindowNative.GetWindowHandle(app.CurrentWindow);
            WinRT.Interop.InitializeWithWindow.Initialize(folderPicker, hWnd);

            var folder = await folderPicker.PickSingleFolderAsync();
            if (folder != null)
            {
                ViewModel.Settings.BackupPath = folder.Path;
                // Update preview of active path (not saved yet, but shows what it will be)
                await ViewModel.SaveBackupPathCommand.ExecuteAsync(null);
            }
        }
    }
}
