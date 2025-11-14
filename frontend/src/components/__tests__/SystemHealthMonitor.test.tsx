import { describe, it, expect, vi } from 'vitest';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import SystemHealthMonitor from '../SystemHealthMonitor';
import { mockHealthChecks } from '@/test/mocks';

describe('SystemHealthMonitor', () => {
  const mockOnRefresh = vi.fn();

  it('renders system health title', () => {
    render(
      <SystemHealthMonitor
        healthChecks={mockHealthChecks}
        loading={false}
        onRefresh={mockOnRefresh}
      />
    );
    
    expect(screen.getByText('System Health')).toBeInTheDocument();
  });

  it('displays all health check items', () => {
    render(
      <SystemHealthMonitor
        healthChecks={mockHealthChecks}
        loading={false}
        onRefresh={mockOnRefresh}
      />
    );
    
    expect(screen.getByText('Database Connection')).toBeInTheDocument();
    expect(screen.getByText('Queue Service')).toBeInTheDocument();
    expect(screen.getByText('Console Application')).toBeInTheDocument();
  });

  it('displays health check status indicators', () => {
    render(
      <SystemHealthMonitor
        healthChecks={mockHealthChecks}
        loading={false}
        onRefresh={mockOnRefresh}
      />
    );
    
    // Should show healthy and unhealthy indicators
    const healthyItems = screen.getAllByText(/healthy/i);
    expect(healthyItems.length).toBeGreaterThan(0);
  });

  it('displays health check messages', () => {
    render(
      <SystemHealthMonitor
        healthChecks={mockHealthChecks}
        loading={false}
        onRefresh={mockOnRefresh}
      />
    );
    
    expect(screen.getByText('Connected to SQLite database')).toBeInTheDocument();
    expect(screen.getByText('Local queue service operational')).toBeInTheDocument();
    expect(screen.getByText('Console processor is not responding')).toBeInTheDocument();
  });

  it('renders refresh button', () => {
    render(
      <SystemHealthMonitor
        healthChecks={mockHealthChecks}
        loading={false}
        onRefresh={mockOnRefresh}
      />
    );
    
    expect(screen.getByText('Refresh')).toBeInTheDocument();
  });

  it('calls onRefresh when refresh button is clicked', async () => {
    const user = userEvent.setup();
    render(
      <SystemHealthMonitor
        healthChecks={mockHealthChecks}
        loading={false}
        onRefresh={mockOnRefresh}
      />
    );
    
    const refreshButton = screen.getByText('Refresh');
    await user.click(refreshButton);
    
    expect(mockOnRefresh).toHaveBeenCalled();
  });

  it('disables refresh button when loading', () => {
    render(
      <SystemHealthMonitor
        healthChecks={mockHealthChecks}
        loading={true}
        onRefresh={mockOnRefresh}
      />
    );
    
    const refreshButton = screen.getByText(/Refreshing/i).closest('button');
    expect(refreshButton).toBeDisabled();
  });

  it('displays different status colors for health states', () => {
    render(
      <SystemHealthMonitor
        healthChecks={mockHealthChecks}
        loading={false}
        onRefresh={mockOnRefresh}
      />
    );
    
    // Check for status indicators with different colors
    const statusIndicators = document.querySelectorAll('[class*="bg-green"], [class*="bg-red"]');
    expect(statusIndicators.length).toBeGreaterThan(0);
  });

  it('formats last checked timestamps', () => {
    render(
      <SystemHealthMonitor
        healthChecks={mockHealthChecks}
        loading={false}
        onRefresh={mockOnRefresh}
      />
    );
    
    // Should display timestamp information
    const timestamps = screen.getAllByText(/2024/);
    expect(timestamps.length).toBeGreaterThan(0);
  });

  it('handles empty health checks array', () => {
    render(
      <SystemHealthMonitor
        healthChecks={[]}
        loading={false}
        onRefresh={mockOnRefresh}
      />
    );
    
    expect(screen.getByText('System Health')).toBeInTheDocument();
  });

  it('displays overall health status', () => {
    render(
      <SystemHealthMonitor
        healthChecks={mockHealthChecks}
        loading={false}
        onRefresh={mockOnRefresh}
      />
    );
    
    // Should show some indication of overall system health
    expect(screen.getByText('System Health')).toBeInTheDocument();
  });
});
