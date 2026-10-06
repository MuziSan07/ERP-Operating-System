namespace Erpos.Domain.Enums;

/// <summary>
/// Unrestricted: general purpose. Restricted: donor-specified purpose (every grant has one). Zakat: may only reach
/// Zakat-eligible beneficiaries. Endowment: capital is kept intact and can't be spent.
/// </summary>
public enum FundKind { Unrestricted = 1, Restricted = 2, Zakat = 3, Endowment = 4 }

public enum DonorType { Individual = 1, Corporate = 2, Foundation = 3, Institutional = 4, Government = 5 }

public enum GrantStatus { Proposal = 1, Active = 2, Closed = 3, Cancelled = 4 }

public enum ReportingFrequency { Monthly = 1, Quarterly = 2, SemiAnnual = 3, Annual = 4, EndOnly = 5 }

public enum BudgetCategory { Personnel = 1, Activities = 2, Assistance = 3, Equipment = 4, Travel = 5, Administration = 6, Other = 7 }

public enum DonationMethod { Cash = 1, BankTransfer = 2, Cheque = 3, Online = 4 }

/// <summary>Functional classification for the statement of functional expenses.</summary>
public enum FunctionalCategory { Program = 1, ManagementGeneral = 2, Fundraising = 3 }


public enum AssistanceType { Cash = 1, InKind = 2, Service = 3 }
