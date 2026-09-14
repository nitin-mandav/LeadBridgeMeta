export type GhlTargetFieldType = 0 | 1 | 2; // StandardContactField | CustomField | Tag

export const GhlTargetFieldTypeLabels: Record<GhlTargetFieldType, string> = {
  0: 'Standard field',
  1: 'Custom field',
  2: 'Tag',
};

export interface FieldMapping {
  id: string;
  metaLeadFormId: string | null;
  metaFieldKey: string;
  targetType: GhlTargetFieldType;
  ghlFieldKey: string;
}

export interface FieldMappingRequest {
  metaLeadFormId: string | null;
  metaFieldKey: string;
  targetType: GhlTargetFieldType;
  ghlFieldKey: string;
}
