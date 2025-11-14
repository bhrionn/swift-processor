import { vi } from 'vitest';
import type { Message, SystemStatus } from '@/types/Message';
import { MessageStatus } from '@/types/Message';

// Mock SignalR connection
export const mockSignalRConnection = {
  start: vi.fn().mockResolvedValue(undefined),
  stop: vi.fn().mockResolvedValue(undefined),
  on: vi.fn(),
  off: vi.fn(),
  invoke: vi.fn(),
  state: 'Connected',
};

// Mock API Client
export const mockApiClient = {
  getMessages: vi.fn(),
  getMessageById: vi.fn(),
  getSystemStatus: vi.fn(),
  restartProcessor: vi.fn(),
  startProcessor: vi.fn(),
  stopProcessor: vi.fn(),
  updateTestMode: vi.fn(),
};

// Mock Messages
export const mockMessages: Message[] = [
  {
    id: '123e4567-e89b-12d3-a456-426614174000',
    messageType: 'MT103',
    rawMessage: '{1:F01BANKBEBBAXXX0000000000}{2:I103BANKGB2LXXXXN}{4:\n:20:REFERENCE123\n:23B:CRED\n:32A:231103EUR1000,00\n:50K:/12345678\nJOHN DOE\n:59:/87654321\nJANE SMITH\n:71A:SHA\n-}',
    status: MessageStatus.Processed,
    processedAt: new Date('2024-01-15T10:30:00Z'),
    errorDetails: null,
    parsedData: {
      transactionReference: 'REFERENCE123',
      bankOperationCode: 'CRED',
      valueDate: '2023-11-03',
      currency: 'EUR',
      amount: 1000.00,
    },
  },
  {
    id: '223e4567-e89b-12d3-a456-426614174001',
    messageType: 'MT103',
    rawMessage: '{1:F01BANKBEBBAXXX0000000000}{2:I103BANKGB2LXXXXN}{4:\n:20:REFERENCE456\n:23B:CRED\n:32A:231104USD2500,00\n:50K:/98765432\nALICE JONES\n:59:/11223344\nBOB BROWN\n:71A:OUR\n-}',
    status: MessageStatus.Failed,
    processedAt: new Date('2024-01-15T11:45:00Z'),
    errorDetails: 'Invalid field format',
    parsedData: null,
  },
];

// Mock System Status
export const mockSystemStatus: SystemStatus = {
  status: 'Running',
  messagesProcessed: 150,
  messagesFailed: 5,
  lastProcessedAt: new Date('2024-01-15T12:00:00Z'),
  uptime: '2 days, 5 hours',
  consoleAppHealthy: true,
};

// Mock Health Checks
export const mockHealthChecks = [
  {
    name: 'Database Connection',
    status: 'healthy' as const,
    message: 'Connected to SQLite database',
    lastChecked: new Date('2024-01-15T12:00:00Z'),
  },
  {
    name: 'Queue Service',
    status: 'healthy' as const,
    message: 'Local queue service operational',
    lastChecked: new Date('2024-01-15T12:00:00Z'),
  },
  {
    name: 'Console Application',
    status: 'unhealthy' as const,
    message: 'Console processor is not responding',
    lastChecked: new Date('2024-01-15T12:00:00Z'),
  },
];
