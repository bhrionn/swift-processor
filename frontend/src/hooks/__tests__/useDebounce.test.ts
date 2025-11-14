import { describe, it, expect } from 'vitest';
import { renderHook, waitFor } from '@testing-library/react';
import { useDebounce } from '../useDebounce';

describe('useDebounce', () => {
  it('returns initial value immediately', () => {
    const { result } = renderHook(() => useDebounce('initial', 500));
    
    expect(result.current).toBe('initial');
  });

  it('debounces value changes', async () => {
    const { result, rerender } = renderHook(
      ({ value, delay }) => useDebounce(value, delay),
      { initialProps: { value: 'initial', delay: 100 } }
    );
    
    expect(result.current).toBe('initial');
    
    // Update value
    rerender({ value: 'updated', delay: 100 });
    
    // Value should not change immediately
    expect(result.current).toBe('initial');
    
    // Wait for debounce
    await waitFor(() => {
      expect(result.current).toBe('updated');
    }, { timeout: 200 });
  });

  it('cancels previous timeout on rapid changes', async () => {
    const { result, rerender } = renderHook(
      ({ value, delay }) => useDebounce(value, delay),
      { initialProps: { value: 'initial', delay: 100 } }
    );
    
    // First update
    rerender({ value: 'first', delay: 100 });
    
    // Second update before first completes
    await new Promise(resolve => setTimeout(resolve, 50));
    rerender({ value: 'second', delay: 100 });
    
    // Wait for debounce to complete
    await waitFor(() => {
      expect(result.current).toBe('second');
    }, { timeout: 200 });
  });

  it('handles different delay values', async () => {
    const { result, rerender } = renderHook(
      ({ value, delay }) => useDebounce(value, delay),
      { initialProps: { value: 'initial', delay: 100 } }
    );
    
    rerender({ value: 'updated', delay: 100 });
    
    await waitFor(() => {
      expect(result.current).toBe('updated');
    }, { timeout: 200 });
  });

  it('works with different value types', async () => {
    const { result, rerender } = renderHook(
      ({ value, delay }) => useDebounce(value, delay),
      { initialProps: { value: 123, delay: 100 } }
    );
    
    expect(result.current).toBe(123);
    
    rerender({ value: 456, delay: 100 });
    
    await waitFor(() => {
      expect(result.current).toBe(456);
    }, { timeout: 200 });
  });
});
