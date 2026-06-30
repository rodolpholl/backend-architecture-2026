using System.ComponentModel.DataAnnotations;

namespace FinControl.Transactions.Core.Domain.Enums
{
    public enum TransactionType
    {
        [Display(Name = "Debit")]
        Debit = 1,
        [Display(Name = "Credit")]
        Credit = 2
    }
}