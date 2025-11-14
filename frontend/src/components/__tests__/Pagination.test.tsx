import { describe, it, expect, vi } from 'vitest';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import Pagination from '../Pagination';

describe('Pagination', () => {
  const mockOnPageChange = vi.fn();

  it('renders pagination controls', () => {
    render(
      <Pagination
        currentPage={1}
        totalItems={100}
        itemsPerPage={20}
        onPageChange={mockOnPageChange}
      />
    );
    
    expect(screen.getByText('Previous')).toBeInTheDocument();
    expect(screen.getByText('Next')).toBeInTheDocument();
  });

  it('displays current page information', () => {
    render(
      <Pagination
        currentPage={2}
        totalItems={100}
        itemsPerPage={20}
        onPageChange={mockOnPageChange}
      />
    );
    
    expect(screen.getByText(/Page 2 of 5/)).toBeInTheDocument();
  });

  it('disables Previous button on first page', () => {
    render(
      <Pagination
        currentPage={1}
        totalItems={100}
        itemsPerPage={20}
        onPageChange={mockOnPageChange}
      />
    );
    
    const previousButton = screen.getByText('Previous').closest('button');
    expect(previousButton).toBeDisabled();
  });

  it('disables Next button on last page', () => {
    render(
      <Pagination
        currentPage={5}
        totalItems={100}
        itemsPerPage={20}
        onPageChange={mockOnPageChange}
      />
    );
    
    const nextButton = screen.getByText('Next').closest('button');
    expect(nextButton).toBeDisabled();
  });

  it('calls onPageChange with correct page when Previous is clicked', async () => {
    const user = userEvent.setup();
    render(
      <Pagination
        currentPage={3}
        totalItems={100}
        itemsPerPage={20}
        onPageChange={mockOnPageChange}
      />
    );
    
    const previousButton = screen.getByText('Previous');
    await user.click(previousButton);
    
    expect(mockOnPageChange).toHaveBeenCalledWith(2);
  });

  it('calls onPageChange with correct page when Next is clicked', async () => {
    const user = userEvent.setup();
    render(
      <Pagination
        currentPage={2}
        totalItems={100}
        itemsPerPage={20}
        onPageChange={mockOnPageChange}
      />
    );
    
    const nextButton = screen.getByText('Next');
    await user.click(nextButton);
    
    expect(mockOnPageChange).toHaveBeenCalledWith(3);
  });

  it('calculates total pages correctly', () => {
    render(
      <Pagination
        currentPage={1}
        totalItems={95}
        itemsPerPage={20}
        onPageChange={mockOnPageChange}
      />
    );
    
    // 95 items / 20 per page = 5 pages
    expect(screen.getByText(/Page 1 of 5/)).toBeInTheDocument();
  });

  it('handles single page correctly', () => {
    render(
      <Pagination
        currentPage={1}
        totalItems={10}
        itemsPerPage={20}
        onPageChange={mockOnPageChange}
      />
    );
    
    expect(screen.getByText(/Page 1 of 1/)).toBeInTheDocument();
    
    const previousButton = screen.getByText('Previous').closest('button');
    const nextButton = screen.getByText('Next').closest('button');
    
    expect(previousButton).toBeDisabled();
    expect(nextButton).toBeDisabled();
  });

  it('displays item range information', () => {
    render(
      <Pagination
        currentPage={2}
        totalItems={100}
        itemsPerPage={20}
        onPageChange={mockOnPageChange}
      />
    );
    
    // Page 2 should show items 21-40 of 100
    expect(screen.getByText(/21-40 of 100/)).toBeInTheDocument();
  });
});
