export interface EmailConnection {
  id: string;
  email: string;
  isActive: boolean;
  createdAtUtc: string;
}

export interface EmailConfigStatus {
  isConfigured: boolean;
  senderEmail: string;
}
