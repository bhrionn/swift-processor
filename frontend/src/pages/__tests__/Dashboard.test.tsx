import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, screen, waitFor } from '@testing-library/react';
import { BrowserRouter } from 'react-router-dom';
import Dashboard from '../Dashboard';
import { mockApiClient, mockSystemStatus, mockSignalRConnection } from '@/test/mocks';

// Mock the API client
vi.mock('@/services/ApiClient', () => ({
  apiClient: mockApiClient,
}));

// Mock the SignalR hook
vi.mock('@/hooks/useSignalR', () => ({
  useSignalR: () => mockSignalRConnection,
}));

const renderDashboard = () => {
  return render(
    <BrowserRouter>
      <Dashboard />
    </BrowserRouter>
  );
};

describe('Dashboard', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    mockApiClient.getSystemStatus.mockResolvedValue({ data: mockSystemStatus });
  });

  it('renders dashboard title and description', () => {
    renderDashboard();
    
    expect(screen.getByText('Dashboard')).toBeInTheDocument();
    expect(screen.getByText('Monitor SWIFT message processing and system status')).toBeInTheDocument();
  });

  it('fetches and displays system status on mount', async () => {
    renderDashboard();

    await waitFor(() => {
      expect(mockApiClient.getSystemStatus).toHaveBeenCalledTimes(1);
    });

    await waitFor(() => {
      expect(screen.getByText('150')).toBeInTheDocument(); // Messages Processed
      expect(screen.getByText('5')).toBeInTheDocument(); // Messages Failed
    });
  });

  it('displays loading state initially', () => {
    mockApiClient.getSystemStatus.mockImplementation(() => new Promise(() => {}));
    renderDashboard();

    // Component should render without crashing during loading
    expect(screen.getByText('Dashboard')).toBeInTheDocument();
  });

  it('displays error message when API call fails', async () => {
    mockApiClient.getSystemStatus.mockRejectedValue(new Error('API Error'));
    renderDashboard();

    await waitFor(() => {
      expect(screen.getByText('Failed to load system status')).toBeInTheDocument();
    });
  });

  it('displays stat cards with correct values', async () => {
    renderDashboard();

    await waitFor(() => {
      expect(screen.getByText('Messages Processed')).toBeInTheDocument();
      expect(screen.getByText('Messages Failed')).toBeInTheDocument();
      expect(screen.getByText('Pending Messages')).toBeInTheDocument();
      expect(screen.getByText('System Status')).toBeInTheDocument();
    });
  });

  it('establishes SignalR connection on mount', async () => {
    renderDashboard();

    await waitFor(() => {
      expect(mockSignalRConnection.start).toHaveBeenCalled();
    });
  });

  it('registers SignalR event handlers', async () => {
    renderDashboard();

    await waitFor(() => {
      expect(mockSignalRConnection.on).toHaveBeenCalledWith('ReceiveSystemStatus', expect.any(Function));
      expect(mockSignalRConnection.on).toHaveBeenCalledWith('ReceiveMessage', expect.any(Function));
    });
  });

  it('cleans up SignalR connection on unmount', async () => {
    const { unmount } = renderDashboard();

    await waitFor(() => {
      expect(mockSignalRConnection.start).toHaveBeenCalled();
    });

    unmount();

    expect(mockSignalRConnection.stop).toHaveBeenCalled();
  });
});
