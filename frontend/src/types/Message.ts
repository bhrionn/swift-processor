export const MessageStatus = {
  Pending: 0,
  Processing: 1,
  Processed: 2,
  Failed: 3,
  DeadLetter: 4
} as const;

export type MessageStatus = typeof MessageStatus[keyof typeof MessageStatus];

export interface Message {
  id: string;
  messageType: string;
  status: MessageStatus;
  processedAt: Date;
  errorDetails?: string;
}

export interface MessageFilter {
  skip: number;
  take: number;
  status?: MessageStatus;
  fromDate?: Date;
  toDate?: Date;
}

export interface PagedResult<T> {
  items: T[];
  totalCount: number;
}

export interface SystemStatus {
  isProcessing: boolean;
  messagesProcessed: number;
  messagesFailed: number;
  lastProcessedAt: Date;
  status: string;
}