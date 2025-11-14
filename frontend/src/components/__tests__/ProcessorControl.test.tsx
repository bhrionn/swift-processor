import { describe, it, expect, vi } from 'vitest';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import ProcessorControl from '../ProcessorControl';
import { mockSystemStatus } from '@/test/mocks';

describe('ProcessorControl', () => {
  const mockProps = {
    status: mockSystemStatus,
    loading: false,
    onRestart: vi.fn(),
    onStop: vi.fn(),
    onStart: vi.fn(),
    restarting: false,
  };

  it('renders processor control title', () => {
    render(<ProcessorControl {...mockProps} />);
    
    expect(screen.getByText('Processor Control')).toBeInTheDocument();
  });

  it('displays processor status', () => {
    render(<ProcessorControl {...mockProps} />);
    
    expect(screen.getByText('Running')).toBeInTheDocument();
  });

  it('renders control buttons', () => {
    render(<ProcessorControl {...mockProps} />);
    
    expect(screen.getByText('Restart')).toBeInTheDocument();
    expect(screen.getByText('Stop')).toBeInTheDocument();
  });

  it('calls onRestart when restart button is clicked', async () => {
    const user = userEvent.setup();
    render(<ProcessorControl {...mockProps} />);
    
    const restartButton = screen.getByText('Restart');
    await user.click(restartButton);
    
    expect(mockProps.onRestart).toHaveBeenCalled();
  });

  it('calls onStop when stop button is clicked', async () => {
    const user = userEvent.setup();
    render(<ProcessorControl {...mockProps} />);
    
    const stopButton = screen.getByText('Stop');
    await user.click(stopButton);
    
    expect(mockProps.onStop).toHaveBeenCalled();
  });

  it('disables buttons when restarting', () => {
    render(<ProcessorControl {...mockProps} restarting={true} />);
    
    const restartButton = screen.getByText(/Restarting/i).closest('button');
    expect(restartButton).toBeDisabled();
  });

  it('shows loading state', () => {
    render(<ProcessorControl {...mockProps} loading={true} />);
    
    // Should show loading indicator or disabled state
    const buttons = screen.getAllByRole('button');
    buttons.forEach(button => {
      expect(button).toBeDisabled();
    });
  });

  it('displays uptime information', () => {
    render(<ProcessorControl {...mockProps} />);
    
    expect(screen.getByText(/2 days, 5 hours/)).toBeInTheDocument();
  });

  it('displays messages processed count', () => {
    render(<ProcessorControl {...mockProps} />);
    
    expect(screen.getByText(/150/)).toBeInTheDocument();
  });

  it('handles null status gracefully', () => {
    render(<ProcessorControl {...mockProps} status={null} />);
    
    expect(screen.getByText('Processor Control')).toBeInTheDocument();
  });
});
