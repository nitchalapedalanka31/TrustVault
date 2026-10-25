# TrustVault: Production-Minded Secure Document-Sharing Platform

TrustVault is an enterprise zero-trust document-sharing platform engineered to enforce strict resource-level authorization, instantaneous permission revocation, anti-oracle information concealment, and a tamper-evident append-only cryptographic audit ledger.

---

## 1. Security Architecture & Threat Model

For the exhaustive formal security model, STRIDE threat taxonomy, and cryptographic hash chain specifications, see:
👉 [docs/SECURITY_MODEL.md](file:///docs/SECURITY_MODEL.md)

### 1.1 Principal Roles & Document Authorization Boundaries
TrustVault strictly decouples system administration from document confidentiality:
- **`ADMIN`**:
  - Manages identities, account activation/deactivation, and global retention policies.
  - Investigates security events, access denials, and verifies cryptographic hash-chain integrity.
  - **Security Invariant**: Administrators **cannot** automatically view or download tenant document contents merely because they are administrators.
- **`OWNER`**:
  - Uploads documents and manages immutable versions.
  - Grants `READ` or `EDIT` permissions to named active colleagues.
  - Revokes grants with instantaneous server-enforced effect.
  - Soft-deletes documents according to retention policy.
- **`VIEWER`**:
  - Discovers and downloads only documents explicitly shared with them.
  - **Security Invariant**: Viewers cannot discover or access documents by guessing IDs, filenames, or direct URLs. Unauthorized requests return `404 Not Found` (Anti-Oracle defense) while recording an `ACCESS_DENIED` audit event.

### 1.2 Access Evaluation Matrix

```
   [User Request: Target Document ID & Required Permission]
                              |
                   [Is User Active & Valid?]
                            /   \
                         No       Yes
                        /           \
                 [DENY 401/403]   [Is Document Deleted or Quarantined?]
                                         /       \
                                      Yes         No
                                      /             \
                             [DENY 404]         [Is User Owner?]
                                                  /        \
                                               Yes          No
                                               /              \
                                         [ALLOW (Full)]   [Active Unrevoked Grant?]
                                                             /          \
                                                          Yes            No
                                                          /                \
                                                  [Permission Check]   [DENY 404 (Anti-Oracle)]
                                                      /        \
                                              Sufficient    Insufficient
                                                 /              \
                                             [ALLOW]        [DENY 404/403]
```

### 1.3 Immediate Revocation Guarantees
- **Authoritative Database State**: TrustVault never relies on distributed caches, long-lived signed download links, or bearer JWT claims for document authorization. Every content streaming request queries authoritative database state with `AsNoTracking()`.
- **Revocation Latency**: Revocation commits synchronously. Subsequent HTTP requests by a revoked user immediately fail authorization.
- **Cache Invalidation**: Content endpoints enforce `Cache-Control: private, no-store, no-cache, must-revalidate` and `X-Content-Type-Options: nosniff` to forbid intermediate and browser disk caching.
- **Physical Boundary**: Revocation prevents all subsequent network access and downloads; TrustVault does not claim the ability to retroactively wipe files already downloaded to a user's local operating system.

### 1.4 Tamper-Evident Audit Ledger
- **Append-Only Architecture**: Events cannot be modified or deleted via ordinary application workflows.
- **Cryptographic Hash Chaining**: Every event computes a SHA-256 hash over an invariant canonical serialization string:
  `{TimestampEpochMs}|{ActorId}|{Action}|{ResourceId}|{Result}|{CorrelationId}|{MetadataJson}|{PreviousHash}`
- **Automated Verification**: The administrative endpoint `POST /api/admin/audit/verify` traverses the entire sequential chain, recalculates canonical hashes, and detects any broken backward pointer or mutated record.

---

## 2. Technology Stack & Verification State

| Layer | Technology | Status & Test Verification |
| :--- | :--- | :--- |
| **Frontend** | Angular 18, Strict TypeScript, Standalone Components, Reactive Forms | Clean compilation (`npm run build` 0 warnings/errors) |
| **Backend** | ASP.NET Core 8 Web API, Entity Framework Core 8, ASP.NET Core Identity | Clean compilation (`dotnet build` 0 warnings/errors) |
| **Database** | PostgreSQL 16 (Production) / SQLite (Zero-Config Local Verification) | Verified with ACID relational integration tests |
| **Storage** | Private Filesystem Storage outside web root (`IFileStorageService`) | Magic-byte sniffing, atomic temporary-to-final moves |
| **Testing** | xUnit, `WebApplicationFactory` with real relational database | **10 / 10 Automated Integration Tests Passing** |
| **Containerization** | Docker Multi-Stage Builds, `docker-compose.yml` | Container definitions complete for API, UI, and Postgres |

---

## 3. Developer & Operator Quick Start

### 3.1 Local Development (Zero-Config Out-of-the-Box)

#### Prerequisites
- .NET 8 LTS SDK
- Node.js 20+ and npm

#### 1. Run Backend Web API
```powershell
cd src/TrustVault.Api
dotnet run
```
*The API automatically seeds development demonstration accounts and listens on `http://localhost:5000`.*

#### 2. Run Angular Frontend
```bash
cd src/trustvault-ui
npm start
```
*The Angular development dashboard serves on `http://localhost:4200` with hot reloading.*

#### 3. Seeded Demonstration Credentials
| Role | Email | Password | Intended Test Purpose |
| :--- | :--- | :--- | :--- |
| **System Admin** | `admin@trustvault.local` | `Admin@TrustVault2026!` | Security telemetry, user provisioning, hash-chain verifier |
| **Document Owner** | `alice@trustvault.local` | `Alice@TrustVault2026!` | Upload documents, manage grants, create versions |
| **Viewer** | `bob@trustvault.local` | `Bob@TrustVault2026!` | Verify tenant isolation, test READ/EDIT grants, test revocation |
| **Viewer** | `charlie@trustvault.local` | `Charlie@TrustVault2026!` | Test account deactivation and immediate session invalidation |

*Note: 1-click credential selector buttons are built into the UI login screen for instant demo switching.*

---

## 4. Automated Security Testing Suite

Run the full automated integration test suite against the live application pipeline:

```powershell
dotnet test tests/TrustVault.Tests/TrustVault.Tests.csproj
```

### Verified Test Matrix:
1. `AnonymousUser_CannotAccessDocuments_ReturnsUnauthorized`: Unauthenticated callers receive 401.
2. `AliceAndBob_TenantIsolation_BobCannotAccessAlicesDocument`: Bob cannot discover or download Alice's confidential documents (returns 404 Anti-Oracle response; records `ACCESS_DENIED` audit event).
3. `DocumentGrantLifecycle_ReadEditAndImmediateRevocation_EnforcedStrictly`: Alice grants READ -> Bob can view and download -> Bob cannot edit -> Alice upgrades to EDIT -> Bob can update metadata and upload version 2 -> Alice revokes grant -> Bob's next download attempt immediately returns 404.
4. `Admin_CannotReadTenantDocumentContent_WithoutExplicitGrant`: Admin inspects security stats but cannot read or download tenant files without an explicit grant.
5. `DeactivatedUser_ImmediatelyBlockedFromAccess`: Deactivating Charlie immediately blocks active sessions and subsequent logins.
6. `DisallowedExtension_ExecutableUpload_Rejected`: Disallowed extensions (`.exe`, `.sh`, `.bat`) rejected.
7. `DisguisedExtension_PolyglotExecutableRenamedToPdf_RejectedByMagicByteSniffer`: Disguised executable files disguised as `.pdf` fail magic-byte sniff validation and are rejected.
8. `DisallowedExtension_ScriptOrHtml_Rejected`: Active scripting formats (`.html`, `.svg`) rejected.
9. `AuditChain_InitialIntegrity_IsValid`: System bootstrap chain passes complete cryptographic verification.
10. `AuditChain_TamperingDetected_WhenDatabaseRecordModified`: Adversarial database record modification causes cryptographic verification failure, pinpointing the corrupted record ID.

---

## 5. Production Deployment Guide (Docker & Kubernetes)

### 5.1 Docker Compose Deployment
Deploy the full enterprise stack (PostgreSQL, ASP.NET Core API, Nginx Angular frontend, and private storage volume) with one command:

```bash
docker compose up -d --build
```

### 5.2 Production Bootstrap & Secret Management
1. Set the initial administrator credentials via environment variables before booting:
   ```bash
   export TRUSTVAULT_ADMIN_EMAIL="secops@enterprise.com"
   export TRUSTVAULT_ADMIN_PASSWORD="StrongProductionPassword123!"
   export ConnectionStrings__DefaultConnection="Host=db.prod;Database=trustvault;Username=tv_user;Password=..."
   ```
2. When `ASPNETCORE_ENVIRONMENT=Production`, all development demo accounts (`alice`, `bob`, `charlie`) are strictly suppressed and will **not** be seeded.
3. Private storage is mounted to `/var/trustvault/storage` outside the Nginx web root and served exclusively through authenticated, authorized streaming endpoints.
