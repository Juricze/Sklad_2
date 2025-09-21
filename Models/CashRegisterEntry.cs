using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Sklad_2.Models
{
    public partial class CashRegisterEntry : ObservableObject
    {
        [Key]
        [ObservableProperty]
        private int id;

        [ObservableProperty]
        private DateTime timestamp;

        [ObservableProperty]
        private EntryType type;

        [ObservableProperty]
        private decimal amount;

        [ObservableProperty]
        private string description;

        [ObservableProperty]
        private decimal currentCashInTill;
    }
}