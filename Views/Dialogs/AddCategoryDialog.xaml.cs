using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Sklad_2.Models;

namespace Sklad_2.Views.Dialogs
{
    public sealed partial class AddCategoryDialog : ContentDialog
    {
        public string CategoryName => NameTextBox.Text;
        public string CategoryDescription => DescriptionTextBox.Text ?? string.Empty;

        private bool _isEditMode;
        private ProductCategory _editingCategory;

        public AddCategoryDialog()
        {
            this.InitializeComponent();
            _isEditMode = false;
            Title = "Přidat kategorii";
        }

        public void SetEditMode(ProductCategory category)
        {
            _isEditMode = true;
            _editingCategory = category;
            Title = "Upravit kategorii";

            NameTextBox.Text = category.Name;
            DescriptionTextBox.Text = category.Description;
        }

        public ProductCategory GetCategory()
        {
            if (_isEditMode && _editingCategory != null)
            {
                _editingCategory.Name = CategoryName;
                _editingCategory.Description = CategoryDescription;
                return _editingCategory;
            }
            else
            {
                return new ProductCategory
                {
                    Name = CategoryName,
                    Description = CategoryDescription
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
                ErrorTextBlock.Text = "Název kategorie nesmí být prázdný.";
                ErrorTextBlock.Visibility = Visibility.Visible;
                args.Cancel = true;
                return;
            }

            if (NameTextBox.Text.Length < 2)
            {
                ErrorTextBlock.Text = "Název kategorie musí obsahovat alespoň 2 znaky.";
                ErrorTextBlock.Visibility = Visibility.Visible;
                args.Cancel = true;
                return;
            }
        }
    }
}
