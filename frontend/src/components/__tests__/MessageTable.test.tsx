import { describe, it, expect, vi } from 'vitest';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import MessageTable from '../MessageTable';
import { mockMessages } from '@/test/mocks';

describe('MessageTable', () => {
  const mockOnMessageClick = vi.fn();

  it('displays loading spinner when loading', () => {
    render(<MessageTable messages={[]} loading={true} onMessageClick={mockOnMessageClick} />);
    
    const spinner = document.querySelector('.animate-spin');
    expect(spinner).toBeInTheDocument();
  });

  it('displays "No messages found" when messages array is empty', () => {
    render(<MessageTable messages={[]} loading={false} onMessageClick={mockOnMessageClick} />);
    
    expect(screen.getByText('No messages found')).toBeInTheDocument();
    expect(screen.getByText('Try adjusting your filters or search criteria')).toBeInTheDocument();
  });

  it('renders table headers correctly', () => {
    render(<MessageTable messages={mockMessages} loading={false} onMessageClick={mockOnMessageClick} />);
    
    expect(screen.getByText('Message ID')).toBeInTheDocument();
    expect(screen.getByText('Type')).toBeInTheDocument();
    expect(screen.getByText('Status')).toBeInTheDocument();
    expect(screen.getByText('Processed At')).toBeInTheDocument();
    expect(screen.getByText('Actions')).toBeInTheDocument();
  });

  it('renders message rows with correct data', () => {
    render(<MessageTable messages={mockMessages} loading={false} onMessageClick={mockOnMessageClick} />);
    
    expect(screen.getAllByText('MT103')).toHaveLength(2);
    expect(screen.getByText('Processed')).toBeInTheDocument();
    expect(screen.getByText('Failed')).toBeInTheDocument();
  });

  it('displays correct status badges', () => {
    render(<MessageTable messages={mockMessages} loading={false} onMessageClick={mockOnMessageClick} />);
    
    const processedBadge = screen.getByText('Processed');
    expect(processedBadge).toHaveClass('bg-green-100', 'text-green-800');
    
    const failedBadge = screen.getByText('Failed');
    expect(failedBadge).toHaveClass('bg-red-100', 'text-red-800');
  });

  it('calls onMessageClick when "View Details" is clicked', async () => {
    const user = userEvent.setup();
    render(<MessageTable messages={mockMessages} loading={false} onMessageClick={mockOnMessageClick} />);
    
    const viewDetailsButtons = screen.getAllByText('View Details');
    await user.click(viewDetailsButtons[0]);
    
    expect(mockOnMessageClick).toHaveBeenCalledWith(mockMessages[0]);
  });

  it('displays truncated message IDs', () => {
    render(<MessageTable messages={mockMessages} loading={false} onMessageClick={mockOnMessageClick} />);
    
    // Should show first 8 characters followed by ...
    expect(screen.getByText('123e4567...')).toBeInTheDocument();
    expect(screen.getByText('223e4567...')).toBeInTheDocument();
  });

  it('formats dates correctly', () => {
    render(<MessageTable messages={mockMessages} loading={false} onMessageClick={mockOnMessageClick} />);
    
    // Check that dates are rendered (exact format may vary by locale)
    const dateElements = screen.getAllByText(/2024/);
    expect(dateElements.length).toBeGreaterThan(0);
  });

  it('applies hover effect to table rows', () => {
    render(<MessageTable messages={mockMessages} loading={false} onMessageClick={mockOnMessageClick} />);
    
    const rows = document.querySelectorAll('tbody tr');
    rows.forEach(row => {
      expect(row).toHaveClass('hover:bg-gray-50');
    });
  });
});
