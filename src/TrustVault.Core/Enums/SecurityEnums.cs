namespace TrustVault.Core.Enums;

public enum UserRole
{
    Admin = 1,
    Owner = 2,
    Viewer = 3
}

public enum DocumentPermission
{
    Read = 1,
    Edit = 2
}

public enum DocumentStatus
{
    Active = 1,
    Quarantined = 2,
    Archived = 3,
    Deleted = 4
}

public static class AuditActionTypes
{
    public const string AuthLoginSuccess = "AUTH_LOGIN_SUCCESS";
    public const string AuthLoginFailed = "AUTH_LOGIN_FAILED";
    public const string AuthLogout = "AUTH_LOGOUT";
    public const string UserCreated = "USER_CREATED";
    public const string UserStatusChanged = "USER_STATUS_CHANGED";
    
    public const string DocumentUploaded = "DOCUMENT_UPLOADED";
    public const string DocumentMetadataUpdated = "DOCUMENT_METADATA_UPDATED";
    public const string DocumentVersionAdded = "DOCUMENT_VERSION_ADDED";
    public const string DocumentDownloaded = "DOCUMENT_DOWNLOADED";
    public const string DocumentViewed = "DOCUMENT_VIEWED";
    public const string DocumentDeleted = "DOCUMENT_DELETED";
    public const string DocumentRestored = "DOCUMENT_RESTORED";
    
    public const string GrantCreated = "GRANT_CREATED";
    public const string GrantRevoked = "GRANT_REVOKED";
    
    public const string AccessDenied = "ACCESS_DENIED";
    public const string UploadValidationFailed = "UPLOAD_VALIDATION_FAILED";
    public const string AuditIntegrityVerified = "AUDIT_INTEGRITY_VERIFIED";

    public const string AiSuggestionGenerated = "AI_SUGGESTION_GENERATED";
    public const string AiSuggestionApproved = "AI_SUGGESTION_APPROVED";
    public const string AiSuggestionRejected = "AI_SUGGESTION_REJECTED";
}

public static class AuditResults
{
    public const string Success = "SUCCESS";
    public const string Denied = "DENIED";
    public const string Failed = "FAILED";
}
