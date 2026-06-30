using System.ComponentModel.DataAnnotations;

namespace FinControl.Entries.Core.Domain.Enums
{
    public enum TransactionType
    {
        [Display(Name = "Debit")]
        Debit = 1,
        [Display(Name = "Credit")]
        Credit = 2
    }
}
