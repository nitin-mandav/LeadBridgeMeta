export type ShopifyTargetFieldType = 0 | 1 | 2 | 3; // StandardCustomerField | Tag | Note | CustomField

export const ShopifyTargetFieldTypeLabels: Record<ShopifyTargetFieldType, string> = {
  0: 'Standard customer field',
  3: 'Custom field / Metafield',
  1: 'Customer tag',
  2: 'Customer note',
};

export interface ShopifyConnection {
  id: string;
  shopDomain: string;
  shopName: string;
  createdAtUtc: string;
}

export interface ShopifyFieldOption {
  key: string;
  label: string;
  type: string;
  isStandard: boolean;
  category?: string;
}

export interface ShopifyFieldMapping {
  id: string;
  metaLeadFormId: string | null;
  metaFieldKey: string;
  targetType: ShopifyTargetFieldType;
  shopifyFieldKey: string;
}

export interface ShopifyFieldMappingRequest {
  metaLeadFormId: string | null;
  metaFieldKey: string;
  targetType: ShopifyTargetFieldType;
  shopifyFieldKey: string;
}
