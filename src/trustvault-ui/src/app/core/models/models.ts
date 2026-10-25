export enum UserRole {
  Admin = 1,
  Owner = 2,
  Viewer = 3
}

export enum DocumentPermission {
  Read = 1,
  Edit = 2
}

export enum DocumentStatus {
  Active = 1,
  Quarantined = 2,
  Archived = 3,
  Deleted = 4
}

export interface UserProfile {
  id: string;
  email: string;
  fullName: string;
  role: UserRole;
  isActive: boolean;
  createdAtUtc: string;
}

export interface DocumentItem {
  id: string;
  title: string;
  originalFileName: string;
  description?: string;
  category: string;
  tags: string;
  status: DocumentStatus;
  currentVersionNumber: number;
  createdAtUtc: string;
  updatedAtUtc: string;
  ownerId: string;
  ownerName: string;
  fileSizeBytes: number;
  contentType: string;
  sha256Hash: string;
  isOwner: boolean;
  permissionLevel?: DocumentPermission;
}

export interface DocumentVersion {
  id: string;
  versionNumber: number;
  fileSizeBytes: number;
  contentType: string;
  sha256Hash: string;
  uploadedByUserId: string;
  uploadedByName: string;
  createdAtUtc: string;
}

export interface DocumentGrant {
  id: string;
  documentId: string;
  userId: string;
  userFullName: string;
  userEmail: string;
  permission: DocumentPermission;
  grantedAtUtc: string;
  revokedAtUtc?: string;
  isActive: boolean;
}

export interface DocumentDetail extends DocumentItem {
  versions: DocumentVersion[];
  grants: DocumentGrant[];
}

export interface PagedResult<T> {
  items: T[];
  totalCount: number;
  pageNumber: number;
  pageSize: number;
  totalPages: number;
}

export interface AuditEventItem {
  id: number;
  timestampUtc: string;
  actorId: string;
  action: string;
  resourceId?: string;
  result: string;
  clientIp?: string;
  correlationId?: string;
  metadataJson: string;
  previousHash: string;
  currentHash: string;
}

export interface AdminStats {
  totalUsers: number;
  totalDocuments: number;
  totalActiveGrants: number;
  totalAuditEvents: number;
  totalAccessDenials: number;
  chainIntegrityValid: boolean;
}

export interface AuditVerificationResult {
  isValid: boolean;
  totalEventsChecked: number;
  errorMessage?: string;
  corruptedEventId?: number;
  checkedAtUtc: string;
}

export interface RecipientSuggestion {
  userId: string;
  fullName: string;
  email: string;
  reason: string;
  confidenceScore: number;
  sourceModel: string;
}
