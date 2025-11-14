import { describe, it, expect, vi } from 'vitest';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import SystemStatusCard from '../SystemStatusCard';
import { mockSystemStatus } from '@/test/mocks';

describe('SystemStatusCard', () => {
  const mockOnRestart = vi.fn();

  it('renders system status card title', () => {
    render(
      <SystemStatusCard
        status={mockSystemStatus}
        loading={false}
        onRestart={mockOnRestart}
        restarting={false}
      />
    );
    
    expect(screen.getByText('System Status')).toBeInTheDocument();
  });

  it('displays processor status', () => {
    render(
      <SystemStatusCard
        status={mockSystemStatus}
        loading={false}
        onRestart={mockOnRestart}
        restarting={false}
      />
    );
    
    expect(screen.getByText('Running')).toBeInTheDocument();
  });

  it('displays messages processed count', () => {
    render(
      <SystemStatusCard
        status={mockSystemStatus}
        loading={false}
        onRestart={mockOnRestart}
        restarting={false}
      />
    );
    
    expect(screen.getByText(/150/)).toBeInTheDocument();
  });

  it('displays messages failed count', () => {
    render(
      <SystemStatusCard
        status={mockSystemStatus}
        loading={false}
        onRestart={mockOnRestart}
        restarting={false}
      />
    );
    
    expect(screen.getByText(/5/)).toBeInTheDocument();
  });

  it('displays uptime information', () => {
    render(
      <SystemStatusCard
        status={mockSystemStatus}
        loading={false}
        onRestart={mockOnRestart}
        restarting={false}
      />
    );
    
    expect(screen.getByText(/2 days, 5 hours/)).toBeInTheDocument();
  });

  it('renders restart button', () => {
    render(
      <SystemStatusCard
        status={mockSystemStatus}
        loading={false}
        onRestart={mockOnRestart}
        restarting={false}
      />
    );
    
    expect(screen.getByText('Restart Processor')).toBeInTheDocument();
  });

  it('calls onRestart when restart button is clicked', async () => {
    const user = userEvent.setup();
    render(
      <SystemStatusCard
        status={mockSystemStatus}
        loading={false}
        onRestart={mockOnRestart}
        restarting={false}
      />
    );
    
    const restartButton = screen.getByText('Restart Processor');
    await user.click(restartButton);
    
    expect(mockOnRestart).toHaveBeenCalled();
  });

  it('disables restart button when restarting', () => {
    render(
      <SystemStatusCard
        status={mockSystemStatus}
        loading={false}
        onRestart={mockOnRestart}
        restarting={true}
      />
    );
    
    const restartButton = screen.getByText(/Restarting/i).closest('button');
    expect(restartButton).toBeDisabled();
  });

  it('shows loading state', () => {
    render(
      <SystemStatusCard
        status={null}
        loading={true}
        onRestart={mockOnRestart}
        restarting={false}
      />
    );
    
    // Should show loading indicator
    const spinner = document.querySelector('.animate-spin');
    expect(spinner).toBeInTheDocument();
  });

  it('displays console app health status', () => {
    render(
      <SystemStatusCard
        status={mockSystemStatus}
        loading={false}
        onRestart={mockOnRestart}
        restarting={false}
      />
    );
    
    // Should indicate console app is healthy
    expect(screen.getByText(/Healthy/i)).toBeInTheDocument();
  });

  it('handles null status gracefully', () => {
    render(
      <SystemStatusCard
        status={null}
        loading={false}
        onRestart={mockOnRestart}
        restarting={false}
      />
    );
    
    expect(screen.getByText('System Status')).toBeInTheDocument();
  });

  it('displays last processed timestamp', () => {
    render(
      <SystemStatusCard
        status={mockSystemStatus}
        loading={false}
        onRestart={mockOnRestart}
        restarting={false}
      />
    );
    
    // Should show last processed time
    expect(screen.getByText(/Last Processed/i)).toBeInTheDocument();
  });
});
