namespace Erpos.Domain.Enums;

public enum AccountType { Asset = 1, Liability = 2, Equity = 3, Income = 4, Expense = 5 }

/// <summary>Finer classification used for statement layout and to find bank, receivable, tax accounts.</summary>
public enum AccountSubType
{
    // Assets
    Cash = 10, Bank = 11, Receivable = 12, TaxReceivable = 13, Inventory = 14, Prepayment = 15, OtherCurrentAsset = 16,
    FixedAsset = 20, AccumulatedDepreciation = 21, IntangibleAsset = 22, OtherNonCurrentAsset = 23,
    // Liabilities
    Payable = 30, TaxPayable = 31, PayrollLiability = 32, AccruedLiability = 33, DeferredIncome = 34, OtherCurrentLiability = 35,
    NonCurrentLiability = 40,
    // Equity
    Capital = 50, RetainedEarnings = 51, Reserves = 52,
    // Income
    Revenue = 60, OtherIncome = 61,
    // Expenses
    CostOfSales = 70, OperatingExpense = 71, PayrollExpense = 72, Depreciation = 73, FinanceCost = 74, TaxExpense = 75,
    // Grouping only
    Group = 99
}

public enum JournalStatus { Draft = 1, Posted = 2, Reversed = 3 }

public enum JournalSource { Manual = 1, Invoice = 2, Bill = 3, Payment = 4, Payroll = 5, Reversal = 6, Inventory = 7, GoodsReceipt = 8, Ngo = 9 }

public enum DocumentKind { Invoice = 1, Bill = 2 }

public enum DocumentStatus { Draft = 1, Open = 2, PartiallyPaid = 3, Paid = 4, Void = 5 }

public enum PaymentKind { Receipt = 1, Payment = 2 }

public enum ReconciliationStatus { Draft = 1, Completed = 2 }
