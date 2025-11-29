using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Sklad_2.ViewModels;
using System;
using System.Collections.Generic;
using Windows.Storage.Pickers;
using WinRT.Interop;

namespace Sklad_2.Views
{
    public sealed partial class NovyProduktPage : Page
    {
        public NovyProduktViewModel ViewModel { get; }

        public NovyProduktPage()
        {
            // IMPORTANT: ViewModel must be set BEFORE InitializeComponent() for x:Bind to work properly
            ViewModel = (Application.Current as App).Services.GetRequiredService<NovyProduktViewModel>();

            this.InitializeComponent();
        }

        private async void SelectImageButton_Click(object sender, RoutedEventArgs e)
        {
            var picker = new FileOpenPicker();
            picker.FileTypeFilter.Add(".jpg");
            picker.FileTypeFilter.Add(".jpeg");
            picker.FileTypeFilter.Add(".png");
            picker.FileTypeFilter.Add(".bmp");
            picker.ViewMode = PickerViewMode.Thumbnail;
            picker.SuggestedStartLocation = PickerLocationId.PicturesLibrary;

            // Get the window handle and initialize picker
            var app = Application.Current as App;
            var hwnd = WindowNative.GetWindowHandle(app.CurrentWindow);
            InitializeWithWindow.Initialize(picker, hwnd);

            var file = await picker.PickSingleFileAsync();
            if (file != null)
            {
                await ViewModel.SetPendingImageAsync(file);
            }
        }
    }
}
