---
inclusion: always
---

# TypeScript Best Practices

## Code Style and Formatting

### TypeScript Configuration
- Use strict TypeScript configuration with `"strict": true`
- Enable `"noImplicitAny": true` and `"strictNullChecks": true`
- Use `"exactOptionalPropertyTypes": true` for precise type checking
- Configure path mapping for clean imports: `"@/*": ["./src/*"]`

### Naming Conventions
- Use PascalCase for interfaces, types, classes, and enums: `MessageProcessor`, `SwiftMessage`
- Use camelCase for variables, functions, and methods: `processMessage`, `messageId`
- Use SCREAMING_SNAKE_CASE for constants: `MAX_RETRY_ATTEMPTS`
- Prefix interfaces with 'I' only when distinguishing from implementation: `IMessageService`

### Type Definitions
```typescript
// Prefer explicit return types for functions
function processMessage(message: string): Promise<ProcessingResult> {
  // implementation
}

// Use readonly for immutable data
interface ReadonlyMessage {
  readonly id: string;
  readonly content: string;
  readonly timestamp: Date;
}

// Use union types for controlled values
type MessageStatus = 'pending' | 'processed' | 'failed';

// Use generic constraints appropriately
interface Repository<T extends { id: string }> {
  findById(id: string): Promise<T | null>;
}
```

## React Best Practices

### Component Structure
```typescript
// Use functional components with TypeScript
interface MessageListProps {
  messages: Message[];
  onMessageSelect: (message: Message) => void;
  loading?: boolean;
}

export const MessageList: React.FC<MessageListProps> = ({
  messages,
  onMessageSelect,
  loading = false
}) => {
  // Component implementation
};
```

### State Management
- Use `useState` with explicit types: `const [messages, setMessages] = useState<Message[]>([])`
- Prefer `useCallback` and `useMemo` for performance optimization
- Use custom hooks for reusable logic: `useMessageProcessor`, `useSignalR`

### Error Handling
```typescript
// Use Result pattern for error handling
type Result<T, E = Error> = 
  | { success: true; data: T }
  | { success: false; error: E };

// Implement proper error boundaries
class MessageErrorBoundary extends React.Component<Props, State> {
  // Error boundary implementation
}
```

## API Integration

### HTTP Client Configuration
```typescript
// Use axios with proper typing
interface ApiResponse<T> {
  data: T;
  status: number;
  message?: string;
}

class MessageApiClient {
  private readonly baseUrl: string;
  
  async getMessages(filter: MessageFilter): Promise<ApiResponse<Message[]>> {
    // Implementation with proper error handling
  }
}
```

### SignalR Integration
```typescript
// Type-safe SignalR hub connections
interface MessageHub {
  ReceiveMessage: (message: Message) => void;
  ReceiveSystemStatus: (status: SystemStatus) => void;
}

const useSignalR = () => {
  const [connection, setConnection] = useState<HubConnection | null>(null);
  
  useEffect(() => {
    const newConnection = new HubConnectionBuilder()
      .withUrl('/messageHub')
      .build();
    
    setConnection(newConnection);
    
    return () => {
      newConnection.stop();
    };
  }, []);
  
  return connection;
};
```

## Performance Optimization

### Bundle Optimization
- Use dynamic imports for code splitting: `const Component = lazy(() => import('./Component'))`
- Implement proper tree shaking with ES modules
- Use React.memo for expensive components
- Implement virtual scrolling for large lists

### Memory Management
- Clean up subscriptions in useEffect cleanup
- Use AbortController for cancelling HTTP requests
- Implement proper cleanup for SignalR connections

## Testing Standards

### Unit Testing
```typescript
// Use Jest with TypeScript
describe('MessageProcessor', () => {
  it('should process valid MT103 message', async () => {
    const processor = new MessageProcessor();
    const result = await processor.process(validMT103Message);
    
    expect(result.success).toBe(true);
    expect(result.data.status).toBe('processed');
  });
});

// Mock external dependencies properly
jest.mock('../services/ApiClient', () => ({
  MessageApiClient: jest.fn().mockImplementation(() => ({
    getMessages: jest.fn().mockResolvedValue({ data: [] })
  }))
}));
```

### Component Testing
```typescript
// Use React Testing Library
import { render, screen, fireEvent } from '@testing-library/react';

test('MessageList displays messages correctly', () => {
  const mockMessages = [
    { id: '1', content: 'Test message', status: 'processed' }
  ];
  
  render(<MessageList messages={mockMessages} onMessageSelect={jest.fn()} />);
  
  expect(screen.getByText('Test message')).toBeInTheDocument();
});
```

## Security Considerations

### Input Validation
```typescript
// Use Zod for runtime type validation
import { z } from 'zod';

const MessageSchema = z.object({
  id: z.string().uuid(),
  content: z.string().min(1).max(1000),
  type: z.enum(['MT103', 'MT102'])
});

type Message = z.infer<typeof MessageSchema>;
```

### XSS Prevention
- Always sanitize user input before rendering
- Use proper Content Security Policy headers
- Validate all data from external sources

## Build and Development

### Package.json Scripts
```json
{
  "scripts": {
    "dev": "vite",
    "build": "tsc && vite build",
    "preview": "vite preview",
    "test": "jest",
    "test:watch": "jest --watch",
    "lint": "eslint src --ext ts,tsx --report-unused-disable-directives --max-warnings 0",
    "type-check": "tsc --noEmit"
  }
}
```

### ESLint Configuration
```json
{
  "extends": [
    "@typescript-eslint/recommended",
    "@typescript-eslint/recommended-requiring-type-checking",
    "plugin:react-hooks/recommended"
  ],
  "rules": {
    "@typescript-eslint/no-unused-vars": "error",
    "@typescript-eslint/explicit-function-return-type": "warn",
    "react-hooks/exhaustive-deps": "error"
  }
}
```

## File Organization

### Project Structure
```
src/
├── components/          # Reusable UI components
│   ├── common/         # Generic components
│   └── message/        # Message-specific components
├── hooks/              # Custom React hooks
├── services/           # API clients and business logic
├── types/              # TypeScript type definitions
├── utils/              # Utility functions
├── stores/             # State management
└── __tests__/          # Test files
```

### Import Organization
```typescript
// 1. External libraries
import React, { useState, useEffect } from 'react';
import axios from 'axios';

// 2. Internal modules (absolute imports)
import { MessageService } from '@/services/MessageService';
import { Message } from '@/types/Message';

// 3. Relative imports
import './MessageList.css';
```

## Documentation Standards

### JSDoc Comments
```typescript
/**
 * Processes a SWIFT message and returns the result
 * @param message - The raw SWIFT message string
 * @param options - Processing options
 * @returns Promise resolving to processing result
 * @throws {ValidationError} When message format is invalid
 */
async function processMessage(
  message: string, 
  options: ProcessingOptions
): Promise<ProcessingResult> {
  // Implementation
}
```

### README Requirements
- Include setup instructions with exact Node.js version requirements
- Document all available scripts and their purposes
- Provide examples of common development tasks
- Include troubleshooting section for common issues