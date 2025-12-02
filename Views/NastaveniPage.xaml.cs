using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Animation;
using Sklad_2.Models;
using Sklad_2.ViewModels;
using Sklad_2.Views.Dialogs;
using System;
using System.Diagnostics;
using System.Threading.Tasks;

namespace Sklad_2.Views
{
    public sealed partial class NastaveniPage : Page
    {
        public NastaveniViewModel ViewModel { get; }
        public CategoryManagementViewModel CategoryVM { get; }
        public UserManagementViewModel UserMgmtVM { get; }
        private Storyboard _blinkAnimation;

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
            
            // Listen for property changes on ViewModel
            ViewModel.PropertyChanged += ViewModel_PropertyChanged;
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
            // Win10 compatibility - initial delay to ensure UI is ready
            await Task.Delay(200);

            var dialog = new AddEditUserDialog();
            dialog.XamlRoot = this.XamlRoot;

            ContentDialogResult result = ContentDialogResult.None;

            // Win10 compatibility - retry if dialog fails due to concurrent dialog or async issues
            int maxRetries = 5; // Increased retries for Win10
            for (int attempt = 0; attempt < maxRetries; attempt++)
            {
                try
                {
                    if (attempt > 0)
                    {
                        // Exponential backoff for Win10
                        int delayMs = 300 + (attempt * 200);
                        Debug.WriteLine($"NastaveniPage: Retrying add dialog after {delayMs}ms delay...");
                        await Task.Delay(delayMs);
                    }

                    result = await dialog.ShowAsync();
                    break; // Success
                }
                catch (System.Runtime.InteropServices.COMException ex) when (attempt < maxRetries - 1)
                {
                    Debug.WriteLine($"NastaveniPage: Add dialog attempt {attempt + 1} failed (COM): {ex.Message} (HResult: 0x{ex.HResult:X})");
                    // Retry with longer delay
                }
                catch (Exception ex) when (attempt < maxRetries - 1)
                {
                    Debug.WriteLine($"NastaveniPage: Add dialog attempt {attempt + 1} failed: {ex.Message}");
                    // Retry
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"NastaveniPage: Error showing add user dialog after all retries: {ex.Message}");
                    return (false, null, null, null, null);
                }
            }

            if (result == ContentDialogResult.Primary)
            {
                return (true, dialog.Username, dialog.DisplayName, dialog.Password, dialog.Role);
            }

            return (false, null, null, null, null);
        }

        private async Task<(bool confirmed, string username, string displayName, string password, string role)> HandleRequestEditUserAsync(User user)
        {
            // Win10 compatibility - initial delay to ensure UI is ready
            await Task.Delay(200);

            var dialog = new AddEditUserDialog();
            dialog.SetEditMode(user);
            dialog.XamlRoot = this.XamlRoot;

            ContentDialogResult result = ContentDialogResult.None;

            // Win10 compatibility - retry if dialog fails due to concurrent dialog or async issues
            int maxRetries = 5; // Increased retries for Win10
            for (int attempt = 0; attempt < maxRetries; attempt++)
            {
                try
                {
                    if (attempt > 0)
                    {
                        // Exponential backoff for Win10
                        int delayMs = 300 + (attempt * 200);
                        Debug.WriteLine($"NastaveniPage: Retrying edit dialog after {delayMs}ms delay...");
                        await Task.Delay(delayMs);
                    }

                    result = await dialog.ShowAsync();
                    break; // Success
                }
                catch (System.Runtime.InteropServices.COMException ex) when (attempt < maxRetries - 1)
                {
                    Debug.WriteLine($"NastaveniPage: Edit dialog attempt {attempt + 1} failed (COM): {ex.Message} (HResult: 0x{ex.HResult:X})");
                    // Retry with longer delay
                }
                catch (Exception ex) when (attempt < maxRetries - 1)
                {
                    Debug.WriteLine($"NastaveniPage: Edit dialog attempt {attempt + 1} failed: {ex.Message}");
                    // Retry
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"NastaveniPage: Error showing edit user dialog after all retries: {ex.Message}");
                    return (false, null, null, null, null);
                }
            }

            if (result == ContentDialogResult.Primary)
            {
                return (true, dialog.Username, dialog.DisplayName, dialog.Password, dialog.Role);
            }

            return (false, null, null, null, null);
        }

        private void ActiveBackupPathText_Loaded(object sender, RoutedEventArgs e)
        {
            // Start or stop blinking animation based on backup path configuration
            var textBlock = sender as TextBlock;
            if (textBlock != null)
            {
                StartOrStopBlinkingAnimation(textBlock);
            }
        }

        private void StartOrStopBlinkingAnimation(TextBlock textBlock)
        {
            // Stop any existing animation first
            if (_blinkAnimation != null)
            {
                _blinkAnimation.Stop();
                _blinkAnimation = null;
                textBlock.Opacity = 1.0; // Reset opacity
            }

            if (!ViewModel.IsBackupPathConfigured)
            {
                // Start blinking for error state
                if (textBlock.Resources.TryGetValue("BlinkErrorAnimation", out var resource))
                {
                    if (resource is Storyboard storyboard)
                    {
                        // Set the target for the animation
                        foreach (var timeline in storyboard.Children)
                        {
                            Storyboard.SetTarget(timeline, textBlock);
                        }
                        _blinkAnimation = storyboard;
                        storyboard.Begin();
                    }
                }
            }
        }

        private void ViewModel_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(ViewModel.IsBackupPathConfigured))
            {
                RefreshBackupPathAnimation();
            }
        }

        private void RefreshBackupPathAnimation()
        {
            // Find the ActiveBackupPathText element and refresh its animation state
            var textBlock = ActiveBackupPathText;
            if (textBlock != null)
            {
                StartOrStopBlinkingAnimation(textBlock);
            }
        }

        public void NavigateToSystemPanel()
        {
            // Programmatically navigate to System panel
            CompanySettingsPanel.Visibility = Visibility.Collapsed;
            VatSettingsPanel.Visibility = Visibility.Collapsed;
            CategoriesPanel.Visibility = Visibility.Collapsed;
            UsersPanel.Visibility = Visibility.Collapsed;
            SystemSettingsPanel.Visibility = Visibility.Visible;
            AboutPanel.Visibility = Visibility.Collapsed;

            // Set the selected item in NavigationView to System
            foreach (var item in NavView.MenuItems)
            {
                if (item is NavigationViewItem navItem && navItem.Tag?.ToString() == "System")
                {
                    NavView.SelectedItem = navItem;
                    break;
                }
            }
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
                ViewModel.BackupPath = folder.Path;
                // Update preview and save
                await ViewModel.SaveBackupPathCommand.ExecuteAsync(null);
            }
        }

        private async void BrowseSecondaryBackupFolderButton_Click(object sender, RoutedEventArgs e)
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
                ViewModel.SecondaryBackupPath = folder.Path;
                // Save and update preview
                await ViewModel.SaveSecondaryBackupPathCommand.ExecuteAsync(null);
            }
        }

        private async void RestoreFromBackupButton_Click(object sender, RoutedEventArgs e)
        {
            // Show warning dialog first
            var warningDialog = new ContentDialog
            {
                Title = "⚠️ VAROVÁNÍ: Obnovení ze zálohy",
                Content = "Tato akce PŘEPÍŠE aktuální databázi daty ze zálohy.\n\n" +
                         "Použijte pouze pokud:\n" +
                         "• Ztratili jste data\n" +
                         "• Databáze je corrupted\n" +
                         "• Chcete vrátit starší verzi\n\n" +
                         "Po obnově MUSÍTE restartovat aplikaci!\n\n" +
                         "Pokračovat?",
                PrimaryButtonText = "Ano, obnovit",
                CloseButtonText = "Zrušit",
                DefaultButton = ContentDialogButton.Close,
                XamlRoot = this.XamlRoot
            };

            var warningResult = await warningDialog.ShowAsync();
            if (warningResult != ContentDialogResult.Primary)
            {
                return; // User cancelled
            }

            // Show folder picker
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
                // Call ViewModel method
                bool success = await ViewModel.RestoreFromBackupAsync(folder.Path);

                if (success)
                {
                    // Show success dialog with restart reminder
                    var successDialog = new ContentDialog
                    {
                        Title = "✅ Obnova dokončena",
                        Content = "Databáze byla úspěšně obnovena ze zálohy.\n\n" +
                                 "⚠️ DŮLEŽITÉ: Pro načtení nových dat MUSÍTE restartovat aplikaci!\n\n" +
                                 "Chcete restartovat nyní?",
                        PrimaryButtonText = "Restartovat aplikaci",
                        CloseButtonText = "Později",
                        DefaultButton = ContentDialogButton.Primary,
                        XamlRoot = this.XamlRoot
                    };

                    var restartResult = await successDialog.ShowAsync();
                    if (restartResult == ContentDialogResult.Primary)
                    {
                        // Restart application
                        System.Environment.Exit(0);
                    }
                }
            }
        }
    }
}
