namespace Erpos.Domain.Enums;

/// <summary>Time &amp; materials: approved billable hours are invoiced. Fixed price: milestones are invoiced. Non-billable: internal.</summary>
public enum ProjectBillingType { TimeAndMaterials = 1, FixedPrice = 2, NonBillable = 3 }

public enum ProjectStatus { Planned = 1, Active = 2, OnHold = 3, Completed = 4, Cancelled = 5 }

public enum WorkItemStatus { Todo = 1, InProgress = 2, Review = 3, Done = 4 }

public enum WorkItemPriority { Low = 1, Medium = 2, High = 3, Urgent = 4 }

public enum TimeEntryStatus { Draft = 1, Submitted = 2, Approved = 3, Rejected = 4, Invoiced = 5 }
