namespace SmartOps.Domain.Modules.Leave;

public enum LeaveLedgerTxnType : short
{
    Accrual = 1,
    Usage = 2,
    Reverse = 3,
    ManualAdjust = 4,
    CarryForward = 5,
    Lapse = 6
}

public enum LeaveAccrualRunStatus : short
{
    Running = 0,
    Success = 1,
    Partial = 2,
    Failed = 3
}
