import { describe, it, expect, vi } from 'vitest';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import MessageFilters from '../MessageFilters';

describe('MessageFilters', () => {
  const mockProps = {
    status: undefined,
    fromDate: '',
    toDate: '',
    searchTerm: '',
    onStatusChange: vi.fn(),
    onFromDateChange: vi.fn(),
    onToDateChange: vi.fn(),
    onSearchChange: vi.fn(),
    onClearFilters: vi.fn(),
  };

  it('renders all filter inputs', () => {
    render(<MessageFilters {...mockProps} />);
    
    expect(screen.getByLabelText('Status')).toBeInTheDocument();
    expect(screen.getByLabelText('From Date')).toBeInTheDocument();
    expect(screen.getByLabelText('To Date')).toBeInTheDocument();
    expect(screen.getByPlaceholderText(/search/i)).toBeInTheDocument();
  });

  it('renders clear filters button', () => {
    render(<MessageFilters {...mockProps} />);
    
    expect(screen.getByText('Clear Filters')).toBeInTheDocument();
  });

  it('calls onStatusChange when status is selected', async () => {
    const user = userEvent.setup();
    render(<MessageFilters {...mockProps} />);
    
    const statusSelect = screen.getByLabelText('Status');
    await user.selectOptions(statusSelect, '2');
    
    expect(mockProps.onStatusChange).toHaveBeenCalledWith(2);
  });

  it('calls onFromDateChange when from date is changed', async () => {
    const user = userEvent.setup();
    render(<MessageFilters {...mockProps} />);
    
    const fromDateInput = screen.getByLabelText('From Date');
    await user.type(fromDateInput, '2024-01-01');
    
    expect(mockProps.onFromDateChange).toHaveBeenCalled();
  });

  it('calls onToDateChange when to date is changed', async () => {
    const user = userEvent.setup();
    render(<MessageFilters {...mockProps} />);
    
    const toDateInput = screen.getByLabelText('To Date');
    await user.type(toDateInput, '2024-01-31');
    
    expect(mockProps.onToDateChange).toHaveBeenCalled();
  });

  it('calls onSearchChange when search input changes', async () => {
    const user = userEvent.setup();
    render(<MessageFilters {...mockProps} />);
    
    const searchInput = screen.getByPlaceholderText(/search/i);
    await user.type(searchInput, 'test');
    
    expect(mockProps.onSearchChange).toHaveBeenCalled();
  });

  it('calls onClearFilters when clear button is clicked', async () => {
    const user = userEvent.setup();
    render(<MessageFilters {...mockProps} />);
    
    const clearButton = screen.getByText('Clear Filters');
    await user.click(clearButton);
    
    expect(mockProps.onClearFilters).toHaveBeenCalled();
  });

  it('displays current filter values', () => {
    const propsWithValues = {
      ...mockProps,
      status: 2,
      fromDate: '2024-01-01',
      toDate: '2024-01-31',
      searchTerm: 'test search',
    };
    
    render(<MessageFilters {...propsWithValues} />);
    
    const statusSelect = screen.getByLabelText('Status') as HTMLSelectElement;
    expect(statusSelect.value).toBe('2');
    
    const fromDateInput = screen.getByLabelText('From Date') as HTMLInputElement;
    expect(fromDateInput.value).toBe('2024-01-01');
    
    const toDateInput = screen.getByLabelText('To Date') as HTMLInputElement;
    expect(toDateInput.value).toBe('2024-01-31');
    
    const searchInput = screen.getByPlaceholderText(/search/i) as HTMLInputElement;
    expect(searchInput.value).toBe('test search');
  });

  it('includes all status options in dropdown', () => {
    render(<MessageFilters {...mockProps} />);
    
    const statusSelect = screen.getByLabelText('Status');
    const options = statusSelect.querySelectorAll('option');
    
    expect(options.length).toBeGreaterThan(1);
    expect(screen.getByText('All')).toBeInTheDocument();
  });
});
