export type LeadEventStatus = 0 | 1 | 2 | 3 | 4; // Received | Fetched | Sent | Failed | Skipped

export const LeadEventStatusLabels: Record<LeadEventStatus, string> = {
  0: 'Received',
  1: 'Fetched',
  2: 'Sent',
  3: 'Failed',
  4: 'Skipped',
};

export interface LeadEvent {
  id: string;
  leadgenId: string;
  formName: string;
  status: LeadEventStatus;
  ghlContactId: string | null;
  errorMessage: string | null;
  retryCount: number;
  receivedAtUtc: string;
  processedAtUtc: string | null;
}
