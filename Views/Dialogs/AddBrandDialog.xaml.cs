using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Sklad_2.Models;

namespace Sklad_2.Views.Dialogs
{
    public sealed partial class AddBrandDialog : ContentDialog
    {
        public string BrandName => NameTextBox.Text;
        public string BrandDescription => DescriptionTextBox.Text ?? string.Empty;

        private bool _isEditMode;
        private Brand _editingBrand;

        public AddBrandDialog()
        {
            this.InitializeComponent();
            _isEditMode = false;
            Title = "Přidat značku";
        }

        public void SetEditMode(Brand brand)
        {
            _isEditMode = true;
            _editingBrand = brand;
            Title = "Upravit značku";

            NameTextBox.Text = brand.Name;
            DescriptionTextBox.Text = brand.Description;
        }

        public Brand GetBrand()
        {
            if (_isEditMode && _editingBrand != null)
            {
                _editingBrand.Name = BrandName;
                _editingBrand.Description = BrandDescription;
                return _editingBrand;
            }
            else
            {
                return new Brand
                {
                    Name = BrandName,
                    Description = BrandDescription
                };
            }
        }

        private void TextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            ErrorTextBlock.Visibility = Visibility.Collapsed;
        }

        private void PrimaryButton_Click(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
            // Validate input
            if (string.IsNullOrWhiteSpace(NameTextBox.Text))
            {
                ErrorTextBlock.Text = "Název značky nesmí být prázdný.";
                ErrorTextBlock.Visibility = Visibility.Visible;
                args.Cancel = true;
                return;
            }

            if (NameTextBox.Text.Length < 2)
            {
                ErrorTextBlock.Text = "Název značky musí obsahovat alespoň 2 znaky.";
                ErrorTextBlock.Visibility = Visibility.Visible;
                args.Cancel = true;
                return;
            }
        }
    }
}
