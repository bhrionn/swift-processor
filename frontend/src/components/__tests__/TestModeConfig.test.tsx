import { describe, it, expect, vi } from 'vitest';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import TestModeConfig from '../TestModeConfig';

describe('TestModeConfig', () => {
  const mockOnToggle = vi.fn();

  it('renders test mode configuration title', () => {
    render(<TestModeConfig enabled={false} onToggle={mockOnToggle} updating={false} />);
    
    expect(screen.getByText('Test Mode Configuration')).toBeInTheDocument();
  });

  it('displays test mode description', () => {
    render(<TestModeConfig enabled={false} onToggle={mockOnToggle} updating={false} />);
    
    expect(screen.getByText(/Enable test mode to generate sample MT103 messages/)).toBeInTheDocument();
  });

  it('renders toggle switch button', () => {
    render(<TestModeConfig enabled={false} onToggle={mockOnToggle} updating={false} />);
    
    const toggle = screen.getByRole('button');
    expect(toggle).toBeInTheDocument();
  });

  it('shows enabled state when enabled is true', () => {
    render(<TestModeConfig enabled={true} onToggle={mockOnToggle} updating={false} />);
    
    // Check for enabled state in the UI
    expect(screen.getByText(/Test mode is currently enabled/)).toBeInTheDocument();
  });

  it('shows disabled state when enabled is false', () => {
    render(<TestModeConfig enabled={false} onToggle={mockOnToggle} updating={false} />);
    
    // Check for disabled state in the UI
    expect(screen.getByText(/Test mode is currently disabled/)).toBeInTheDocument();
  });

  it('calls onToggle with true when toggled on', async () => {
    const user = userEvent.setup();
    render(<TestModeConfig enabled={false} onToggle={mockOnToggle} updating={false} />);
    
    const toggle = screen.getByRole('button');
    await user.click(toggle);
    
    expect(mockOnToggle).toHaveBeenCalledWith(true);
  });

  it('calls onToggle with false when toggled off', async () => {
    const user = userEvent.setup();
    render(<TestModeConfig enabled={true} onToggle={mockOnToggle} updating={false} />);
    
    const toggle = screen.getByRole('button');
    await user.click(toggle);
    
    expect(mockOnToggle).toHaveBeenCalledWith(false);
  });

  it('disables toggle when updating', () => {
    render(<TestModeConfig enabled={false} onToggle={mockOnToggle} updating={true} />);
    
    const toggle = screen.getByRole('button');
    expect(toggle).toBeDisabled();
  });

  it('displays test mode details when enabled', () => {
    render(<TestModeConfig enabled={true} onToggle={mockOnToggle} updating={false} />);
    
    expect(screen.getByText('Test Mode Details')).toBeInTheDocument();
    expect(screen.getByText('10 seconds')).toBeInTheDocument();
  });

  it('displays configuration information', () => {
    render(<TestModeConfig enabled={true} onToggle={mockOnToggle} updating={false} />);
    
    expect(screen.getByText(/Generation Interval/)).toBeInTheDocument();
    expect(screen.getByText(/Message Types/)).toBeInTheDocument();
  });
});
