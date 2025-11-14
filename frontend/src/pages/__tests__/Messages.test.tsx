import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, screen, waitFor, fireEvent } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { BrowserRouter } from 'react-router-dom';
import Messages from '../Messages';
import { mockApiClient, mockMessages } from '@/test/mocks';

// Mock the API client
vi.mock('@/services/ApiClient', () => ({
  apiClient: mockApiClient,
}));

const renderMessages = () => {
  return render(
    <BrowserRouter>
      <Messages />
    </BrowserRouter>
  );
};

describe('Messages', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    mockApiClient.getMessages.mockResolvedValue({
      data: {
        items: mockMessages,
        totalCount: 2,
      },
    });
  });

  it('renders messages page title and description', () => {
    renderMessages();
    
    expect(screen.getByText('Messages')).toBeInTheDocument();
    expect(screen.getByText('View and search processed SWIFT messages')).toBeInTheDocument();
  });

  it('fetches and displays messages on mount', async () => {
    renderMessages();

    await waitFor(() => {
      expect(mockApiClient.getMessages).toHaveBeenCalled();
    });

    await waitFor(() => {
      expect(screen.getByText('MT103')).toBeInTheDocument();
      expect(screen.getByText('Processed')).toBeInTheDocument();
    });
  });

  it('displays loading spinner while fetching messages', async () => {
    mockApiClient.getMessages.mockImplementation(() => new Promise(() => {}));
    renderMessages();

    // Loading spinner should be visible
    const spinner = document.querySelector('.animate-spin');
    expect(spinner).toBeInTheDocument();
  });

  it('displays error message when API call fails', async () => {
    mockApiClient.getMessages.mockRejectedValue(new Error('API Error'));
    renderMessages();

    await waitFor(() => {
      expect(screen.getByText('Failed to load messages')).toBeInTheDocument();
    });
  });

  it('displays "No messages found" when no messages are returned', async () => {
    mockApiClient.getMessages.mockResolvedValue({
      data: {
        items: [],
        totalCount: 0,
      },
    });
    renderMessages();

    await waitFor(() => {
      expect(screen.getByText('No messages found')).toBeInTheDocument();
    });
  });

  it('opens message detail modal when clicking "View Details"', async () => {
    const user = userEvent.setup();
    renderMessages();

    await waitFor(() => {
      expect(screen.getByText('MT103')).toBeInTheDocument();
    });

    const viewDetailsButton = screen.getAllByText('View Details')[0];
    await user.click(viewDetailsButton);

    await waitFor(() => {
      expect(screen.getByText('Message Details')).toBeInTheDocument();
    });
  });

  it('filters messages by status', async () => {
    const user = userEvent.setup();
    renderMessages();

    await waitFor(() => {
      expect(mockApiClient.getMessages).toHaveBeenCalledTimes(1);
    });

    const statusSelect = screen.getByLabelText('Status');
    await user.selectOptions(statusSelect, '2'); // Processed status

    await waitFor(() => {
      expect(mockApiClient.getMessages).toHaveBeenCalledWith(
        expect.objectContaining({ status: 2 })
      );
    });
  });

  it('clears all filters when clicking "Clear Filters"', async () => {
    const user = userEvent.setup();
    renderMessages();

    await waitFor(() => {
      expect(screen.getByText('MT103')).toBeInTheDocument();
    });

    const clearButton = screen.getByText('Clear Filters');
    await user.click(clearButton);

    await waitFor(() => {
      expect(mockApiClient.getMessages).toHaveBeenCalledWith(
        expect.objectContaining({ skip: 0, take: 20 })
      );
    });
  });

  it('handles pagination correctly', async () => {
    const user = userEvent.setup();
    mockApiClient.getMessages.mockResolvedValue({
      data: {
        items: mockMessages,
        totalCount: 50,
      },
    });
    renderMessages();

    await waitFor(() => {
      expect(screen.getByText('MT103')).toBeInTheDocument();
    });

    // Should show pagination controls
    expect(screen.getByText('Next')).toBeInTheDocument();
  });

  it('debounces search input', async () => {
    const user = userEvent.setup();
    renderMessages();

    await waitFor(() => {
      expect(mockApiClient.getMessages).toHaveBeenCalledTimes(1);
    });

    const searchInput = screen.getByPlaceholderText(/search/i);
    await user.type(searchInput, 'test');

    // Should not call API immediately
    expect(mockApiClient.getMessages).toHaveBeenCalledTimes(1);

    // Wait for debounce
    await waitFor(() => {
      expect(mockApiClient.getMessages).toHaveBeenCalledTimes(2);
    }, { timeout: 1000 });
  });
});
