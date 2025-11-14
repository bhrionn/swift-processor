import { describe, it, expect, vi } from 'vitest';
import { renderHook } from '@testing-library/react';
import { useSignalR } from '../useSignalR';

// Mock SignalR module
vi.mock('@microsoft/signalr', () => {
  const mockConnection = {
    start: vi.fn().mockResolvedValue(undefined),
    stop: vi.fn().mockResolvedValue(undefined),
    on: vi.fn(),
    off: vi.fn(),
    invoke: vi.fn(),
    state: 'Connected',
  };

  return {
    HubConnectionBuilder: class {
      withUrl() { return this; }
      withAutomaticReconnect() { return this; }
      build() { return mockConnection; }
    },
    HubConnectionState: {
      Connected: 'Connected',
      Disconnected: 'Disconnected',
      Connecting: 'Connecting',
      Reconnecting: 'Reconnecting',
    },
  };
});

describe('useSignalR', () => {
  it('creates and returns a SignalR connection', () => {
    const { result } = renderHook(() => useSignalR());
    
    expect(result.current).toBeDefined();
    expect(result.current).toHaveProperty('start');
    expect(result.current).toHaveProperty('stop');
    expect(result.current).toHaveProperty('on');
  });

  it('returns the same connection instance on re-render', () => {
    const { result, rerender } = renderHook(() => useSignalR());
    
    const firstConnection = result.current;
    rerender();
    const secondConnection = result.current;
    
    expect(firstConnection).toBe(secondConnection);
  });

  it('connection has required methods', () => {
    const { result } = renderHook(() => useSignalR());
    
    expect(typeof result.current?.start).toBe('function');
    expect(typeof result.current?.stop).toBe('function');
    expect(typeof result.current?.on).toBe('function');
  });
});
