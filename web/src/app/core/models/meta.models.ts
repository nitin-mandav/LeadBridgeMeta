export interface MetaLeadForm {
  id: string;
  formId: string;
  formName: string;
  isActive: boolean;
  ghlConnectionId: string | null;
}

export interface MetaPage {
  id: string;
  pageId: string;
  pageName: string;
  isLeadgenWebhookSubscribed: boolean;
  forms: MetaLeadForm[];
}

export interface MetaConnection {
  id: string;
  facebookUserName: string;
  tokenExpiresAtUtc: string;
  pages: MetaPage[];
}
