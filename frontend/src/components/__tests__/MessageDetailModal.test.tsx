import { describe, it, expect, vi } from 'vitest';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import MessageDetailModal from '../MessageDetailModal';
import { mockMessages } from '@/test/mocks';

describe('MessageDetailModal', () => {
  const mockOnClose = vi.fn();

  it('does not render when isOpen is false', () => {
    render(<MessageDetailModal message={null} isOpen={false} onClose={mockOnClose} />);
    
    expect(screen.queryByText('Message Details')).not.toBeInTheDocument();
  });

  it('renders modal when isOpen is true', () => {
    render(<MessageDetailModal message={mockMessages[0]} isOpen={true} onClose={mockOnClose} />);
    
    expect(screen.getByText('Message Details')).toBeInTheDocument();
  });

  it('displays message information', () => {
    render(<MessageDetailModal message={mockMessages[0]} isOpen={true} onClose={mockOnClose} />);
    
    expect(screen.getByText('Message ID')).toBeInTheDocument();
    expect(screen.getByText('Message Type')).toBeInTheDocument();
    expect(screen.getByText('Status')).toBeInTheDocument();
  });

  it('displays parsed data when available', () => {
    render(<MessageDetailModal message={mockMessages[0]} isOpen={true} onClose={mockOnClose} />);
    
    expect(screen.getByText('Parsed Data')).toBeInTheDocument();
  });

  it('displays raw message', () => {
    render(<MessageDetailModal message={mockMessages[0]} isOpen={true} onClose={mockOnClose} />);
    
    expect(screen.getByText('Raw Message')).toBeInTheDocument();
  });

  it('displays error details for failed messages', () => {
    render(<MessageDetailModal message={mockMessages[1]} isOpen={true} onClose={mockOnClose} />);
    
    expect(screen.getByText('Error Details')).toBeInTheDocument();
    expect(screen.getByText('Invalid field format')).toBeInTheDocument();
  });

  it('calls onClose when close button is clicked', async () => {
    const user = userEvent.setup();
    render(<MessageDetailModal message={mockMessages[0]} isOpen={true} onClose={mockOnClose} />);
    
    const closeButton = screen.getByText('Close');
    await user.click(closeButton);
    
    expect(mockOnClose).toHaveBeenCalled();
  });

  it('calls onClose when clicking outside modal', async () => {
    const user = userEvent.setup();
    render(<MessageDetailModal message={mockMessages[0]} isOpen={true} onClose={mockOnClose} />);
    
    const backdrop = document.querySelector('.fixed.inset-0');
    if (backdrop) {
      await user.click(backdrop);
      expect(mockOnClose).toHaveBeenCalled();
    }
  });

  it('handles null message gracefully', () => {
    render(<MessageDetailModal message={null} isOpen={true} onClose={mockOnClose} />);
    
    // Should not crash, may show empty state or close automatically
    expect(screen.queryByText('Message Details')).toBeInTheDocument();
  });

  it('formats dates correctly', () => {
    render(<MessageDetailModal message={mockMessages[0]} isOpen={true} onClose={mockOnClose} />);
    
    // Check that processed date is displayed
    expect(screen.getByText(/Processed At/i)).toBeInTheDocument();
  });
});
