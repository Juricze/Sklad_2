using CommunityToolkit.Mvvm.ComponentModel;

namespace Sklad_2.Models.Settings
{
    public partial class AppSettings : ObservableObject
    {
        [ObservableProperty]
        private string shopName;

        [ObservableProperty]
        private string shopAddress;

        [ObservableProperty]
        private string companyId;

        [ObservableProperty]
        private string vatId;

        [ObservableProperty]
        private bool isVatPayer;

        [ObservableProperty]
        private string printerPath;
    }
}