using Microsoft.UI.Xaml.Controls;
using Sklad_2.Models;

namespace Sklad_2.Views.Dialogs
{
    public sealed partial class ReturnPreviewDialog : ContentDialog
    {
        public Return ReturnDocument { get; }

        public ReturnPreviewDialog(Return returnDocument)
        {
            this.InitializeComponent();
            ReturnDocument = returnDocument;
        }
    }
}
