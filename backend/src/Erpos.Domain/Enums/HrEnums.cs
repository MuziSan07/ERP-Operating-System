namespace Erpos.Domain.Enums;

public enum Gender { Male = 1, Female = 2, Other = 3 }

public enum EmploymentType { Permanent = 1, Probation = 2, Contract = 3, Intern = 4, DailyWage = 5 }

public enum EmployeeStatus { Active = 1, Resigned = 2, Terminated = 3, Retired = 4 }

public enum AttendanceStatus { Present = 1, Absent = 2, Late = 3, HalfDay = 4, Leave = 5, Holiday = 6, WeeklyOff = 7 }

public enum LeaveStatus { Pending = 1, Approved = 2, Rejected = 3, Cancelled = 4 }

public enum ApprovalStatus { Waiting = 0, Pending = 1, Approved = 2, Rejected = 3, Skipped = 4 }

/// <summary>Who must act on a leave approval step.</summary>
public enum ApproverType
{
    /// <summary>The employee's line manager.</summary>
    LineManager = 1,
    /// <summary>The head of the employee's department.</summary>
    DepartmentHead = 2,
    /// <summary>Anyone holding hr.leave.approve at the employee's entity (HR team).</summary>
    HrPermission = 3,
    /// <summary>One named user.</summary>
    SpecificUser = 4
}

public enum PayComponentKind { Earning = 1, Deduction = 2 }

public enum PayrollRunStatus { Draft = 1, Approved = 2, Posted = 3, Cancelled = 4 }
