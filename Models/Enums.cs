namespace yurt_lojman_yonetim_sistemi.Models;

public enum AccommodationType
{
    Yurt = 1,
    Lojman = 2
}

public enum RoomStatus
{
    Empty = 1,
    PartiallyFull = 2,
    Full = 3,
    Maintenance = 4
}

public enum ApplicationStatus
{
    Pending = 1,
    Approved = 2,
    Rejected = 3,
    EmailVerificationPending = 4,
    UnderReview = 5,
    MissingInformation = 6,
    ApprovedAwaitingActivation = 7,
    Cancelled = 8
}

public enum ApplicationSource
{
    RegisteredUser = 1,
    ExternalApplicant = 2
}

public enum ApplicationTokenPurpose
{
    EmailVerification = 1,
    StatusTracking = 2,
    AccountActivation = 3
}

public enum PaymentStatus
{
    Unpaid = 1,
    Paid = 2,
    Overdue = 3
}

public enum RequestStatus
{
    Open = 1,
    InProgress = 2,
    Resolved = 3,
    Rejected = 4
}

public enum AnnouncementTargetRole
{
    All = 1,
    Student = 2,
    Staff = 3
}
