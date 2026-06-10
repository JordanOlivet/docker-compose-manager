/**
 * Column definition for draggable table headers
 */
export interface ColumnDefinition {
  /** Unique identifier for storage and rendering */
  id: string;
  /** i18n key for the column label */
  labelKey: string;
  /** Sort key (if sortable, omit for non-sortable columns like actions) */
  sortKey?: string;
  /** Optional CSS width */
  width?: string;
  /** If true, column cannot be dragged (reserved for future use) */
  fixed?: boolean;
}

/**
 * Stored column preferences
 */
export interface ColumnPreferences {
  /** Column IDs in display order */
  order: string[];
}

/**
 * Table identifiers for localStorage keys
 */
export type TableId = 'containers' | 'compose-projects' | 'compose-services';
