# TrustVault Security Model & Threat Specification

## 1. Executive Summary & Purpose
TrustVault is a zero-trust, enterprise document-sharing platform engineered to guarantee that document access is strictly bounded by explicit, verifiable, and continuously enforced authorizations. The core architectural invariant is: **no principal—regardless of role, organizational position, or route access—can access or download any document content without an explicit, active, unrevoked grant or direct ownership.**

---

## 2. Role-Based & Resource-Level Authorization Architecture

TrustVault decouples administrative management of users/tenants from document confidentiality. The platform defines three distinct principal roles and two explicit document-level permissions.

### 2.1 Principal Roles

| Role | Permitted Capabilities | Prohibited Capabilities & Security Invariants |
| :--- | :--- | :--- |
| **ADMIN** | • Manage user accounts, activation, and deactivation.<br>• View security events and aggregated document activity metadata.<br>• Inspect permission changes, failed logins, and denied access attempts.<br>• Review cryptographic audit-chain integrity.<br>• Configure retention and security policies. | • **CANNOT** read or download document contents unless explicitly granted access by the owner.<br>• **CANNOT** silently alter or erase audit log records.<br>• **CANNOT** bypass resource-level authorization checks. Any break-glass access requires explicit dual-control authorization, narrow scope, time-limits, and immutable logging. |
| **OWNER** | • Upload documents and generate new versions.<br>• Manage and edit document metadata.<br>• Grant `READ` or `EDIT` access to active named users.<br>• Revoke grants with immediate server-side effect.<br>• Soft-delete and archive owned documents.<br>• View audit history for owned documents. | • **CANNOT** grant permissions to documents owned by other users.<br>• **CANNOT** escalate privileges or assign ADMIN roles.<br>• **CANNOT** access quarantined documents. |
| **VIEWER** | • Search and view metadata for documents explicitly shared with them.<br>• Download document content with an active `READ` grant.<br>• Inspect permitted personal activity history. | • **CANNOT** edit document content or metadata.<br>• **CANNOT** grant or revoke permissions.<br>• **CANNOT** discover or access documents by guessing IDs, hashes, or URLs.<br>• **CANNOT** access deleted or quarantined files. |

### 2.2 Document-Level Permissions

- **`READ`**: Grants permission to retrieve metadata, stream/preview, and download document file content.
- **`EDIT`**: Extends `READ` with the ability to edit permitted document metadata (title, description, tags, category) and upload new document versions. `EDIT` does **not** allow transferring ownership, granting/revoking permissions, or deleting the document.

### 2.3 Access Evaluation Matrix

Every document request undergoes authoritative resource evaluation inside `DocumentAuthorizationService`:

```
                 +-----------------------------------------+
                 | Request Principal & Target Document ID  |
                 +-----------------------------------------+
                                      |
                         [Is Principal Active?]
                                 /  \
                              No     Yes
                             /         \
                       [DENY 401]   [Is Document Deleted or Quarantined?]
                                            /     \
                                         Yes       No
                                         /           \
                                    [DENY 404]    [Is Principal Owner?]
                                                    /     \
                                                 Yes       No
                                                 /           \
                                            [ALLOW]     [Check Active Grant]
                                                          /           \
                                                 Grant Active       No Grant / Expired / Revoked
                                                 & Sufficient                 |
                                                       /                  [DENY 404/403]
                                                   [ALLOW]
```

#### Evaluation Rules:
1. **Principal Status**: The authenticated user must be active (`IsActive == true`). Deactivated users fail immediately with 401 Unauthorized or 403 Forbidden.
2. **Document Lifecycle**: If the document is soft-deleted or quarantined, requests from non-owners or unauthorized callers return `404 Not Found` to prevent metadata leakage.
3. **Owner Dominance**: The document owner has full operational rights (`READ`, `EDIT`, `DELETE`, `MANAGE_PERMISSIONS`).
4. **Explicit Grant Verification**: For non-owners, an unexpired, active record in `DocumentGrants` matching the user ID and document ID with the required level (`READ` or `EDIT`) must exist.
5. **Fail-Closed Default**: In the absence of an active grant, access is strictly denied. Organization-wide roles, folder grouping, or administrative status never imply document content access.
6. **Information Concealment**: For unauthorized access attempts (IDOR probing), TrustVault returns `404 Not Found` rather than `403 Forbidden` for content endpoints, eliminating resource existence oracle attacks while recording a high-severity `ACCESS_DENIED` event in the audit trail.

---

## 3. Threat Model (STRIDE Analysis)

| Threat Category | Potential Attack Vectors | TrustVault Defense & Architectural Controls |
| :--- | :--- | :--- |
| **Spoofing** | • Stolen credentials.<br>• Session hijacking.<br>• Forged identity claims. | • ASP.NET Core Identity with PBKDF2/Argon2 password hashing.<br>• Secure, `HttpOnly`, `SameSite=Strict`, `Secure` authentication cookies.<br>• Anti-CSRF token verification on state-changing mutations.<br>• User deactivation immediately invalidates active sessions via security stamp validation. |
| **Tampering** | • Malicious file upload with disguised extension.<br>• Path traversal (`../../etc/passwd`).<br>• Audit trail manipulation.<br>• Direct database modification. | • Magic-byte MIME sniffing and strict extension allowlists.<br>• Randomized GUID storage keys; original filenames retained purely as sanitized display metadata.<br>• Files stored strictly outside the web root (`/var/trustvault/storage` or private volume).<br>• Tamper-evident cryptographic SHA-256 hash chaining on all audit events with canonical JSON serialization. |
| **Repudiation** | • Actor denies granting access or downloading confidential file.<br>• System administrator denies modifying access rules. | • Comprehensive, append-only audit trail recording actor ID, IP, user-agent, correlation ID, previous event hash, and canonical payload hash.<br>• Continuous audit chain validation endpoint (`/api/admin/audit/verify`) detecting insertions, deletions, or mutations. |
| **Information Disclosure** | • IDOR / BOLA on document endpoints (`/api/documents/{id}/download`).<br>• Search leakage of unauthorized files.<br>• Timing attacks or existence oracles.<br>• Leaked internal paths or stack traces. | • Centralized `DocumentAuthorizationService` evaluated on every resource access.<br>• Search queries filter strictly by `OwnerId == CurrentUser || Grants.Any(g => g.UserId == CurrentUser && g.IsActive)`.<br>• Uniform `404 Not Found` responses for unauthorized documents.<br>• Global exception middleware stripping stack traces and returning sanitized RFC 7807 problem details. |
| **Denial of Service** | • Zip bombs / multi-gigabyte upload exhaustion.<br>• Authentication brute-forcing.<br>• Hash chain recalculation abuse. | • Configurable upload body size limit (e.g., 25MB).<br>• ASP.NET Core rate limiting on login, upload, and verification routes.<br>• Account lockout after 5 consecutive failed authentication attempts. |
| **Elevation of Privilege** | • Standard viewer attempting to update metadata or grant access.<br>• Admin attempting to read document files.<br>• Mass assignment on grant creation. | • Strong role- and policy-based authorization handlers.<br>• Separation of concerns: Admins manage tenants, not content.<br>• Strict DTO modeling preventing mass-assignment of ownership or approval flags. |

---

## 4. Immediate Revocation Semantics & Concurrency Guarantees

1. **Authoritative State Verification**: TrustVault does not rely on distributed caching, JWT lifetimes, or long-lived bearer tokens for resource authorization. Every file read or download request executes an ACID read against the authoritative database.
2. **Revocation Latency**: Revocation takes effect the instant the `RevokeGrant` database transaction commits. Subsequent HTTP requests by the revoked user immediately fail authorization.
3. **In-Flight Stream Handling**: 
   - Downloads are delivered via chunked streaming with continuous authorization cancellation tokens.
   - Response headers include `Cache-Control: private, no-store, no-cache, must-revalidate` and `Pragma: no-cache` to forbid intermediary and browser caching.
4. **Physical Limitation Boundary**: As an explicit security guarantee, TrustVault guarantees that revocation prevents all future access requests; it does not claim to delete files already saved to a client's local storage prior to revocation.

---

## 5. Tamper-Evident Audit Trail Specification

### 5.1 Canonical Event Structure
Every audit event captures:
- `Id`: Sequential 64-bit integer or ULID.
- `TimestampUtc`: ISO-8601 UTC timestamp.
- `ActorId`: Authenticated user GUID or `ANONYMOUS`.
- `Action`: E.g., `DOCUMENT_UPLOAD`, `DOCUMENT_READ`, `ACCESS_DENIED`, `GRANT_REVOKED`.
- `ResourceId`: Target document ID or system entity.
- `Result`: `SUCCESS`, `DENIED`, or `FAILED`.
- `ClientIp`: Client remote IP address.
- `CorrelationId`: Trace identifier for request tracking.
- `MetadataJson`: Sanitized, deterministic JSON dictionary.
- `PreviousHash`: Hex-encoded SHA-256 of the prior sequential event (or genesis seed `0000000000000000000000000000000000000000000000000000000000000000`).
- `CurrentHash`: Hex-encoded SHA-256 computed over the canonical representation:
  `CanonicalString = $"{Id}|{TimestampUtc:O}|{ActorId}|{Action}|{ResourceId}|{Result}|{CorrelationId}|{MetadataJson}|{PreviousHash}"`

### 5.2 Tamper Evidence & Verification
- The chain can be verified end-to-end via an administrative API or CLI tool.
- Any modification of historical records invalidates all subsequent hash links.
- Verification detects:
  - Broken link pointers (`Event[N].PreviousHash != Event[N-1].CurrentHash`).
  - Corrupted record contents (`ComputeHash(Event[N]) != Event[N].CurrentHash`).
  - Sequence gaps or dropped events.
