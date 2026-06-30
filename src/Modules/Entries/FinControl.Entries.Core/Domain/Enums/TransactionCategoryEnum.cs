using System.ComponentModel.DataAnnotations;

namespace FinControl.Entries.Core.Domain.Enums;

public enum TransactionCategory
{
    [Display(Name = "Sale")]
    Sale = 1,

    [Display(Name = "Return")]
    Return = 2,

    [Display(Name = "Cash Supply")]
    CashSupply = 3,

    [Display(Name = "Cash Withdrawal")]
    CashWithdrawal = 4,

    [Display(Name = "Supplier Payment")]
    SupplierPayment = 5,

    [Display(Name = "Debt Collection")]
    DebtCollection = 6,

    [Display(Name = "Others")]
    Others = 7
}

