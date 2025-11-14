import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { BrowserRouter } from 'react-router-dom';
import Settings from '../Settings';
import { mockApiClient, mockSystemStatus } from '@/test/mocks';

// Mock the API client
vi.mock('@/services/ApiClient', () => ({
  apiClient: mockApiClient,
}));

const renderSettings = () => {
  return render(
    <BrowserRouter>
      <Settings />
    </BrowserRouter>
  );
};

describe('Settings', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    mockApiClient.getSystemStatus.mockResolvedValue({ data: mockSystemStatus });
  });

  it('renders settings page title and description', () => {
    renderSettings();
    
    expect(screen.getByText('Settings')).toBeInTheDocument();
    expect(screen.getByText('Manage system configuration and monitor health')).toBeInTheDocument();
  });

  it('fetches system status on mount', async () => {
    renderSettings();

    await waitFor(() => {
      expect(mockApiClient.getSystemStatus).toHaveBeenCalledTimes(1);
    });
  });

  it('displays processor control section', async () => {
    renderSettings();

    await waitFor(() => {
      expect(screen.getByText('Processor Control')).toBeInTheDocument();
    });
  });

  it('displays test mode configuration section', async () => {
    renderSettings();

    await waitFor(() => {
      expect(screen.getByText('Test Mode Configuration')).toBeInTheDocument();
    });
  });

  it('displays system health monitor section', async () => {
    renderSettings();

    await waitFor(() => {
      expect(screen.getByText('System Health')).toBeInTheDocument();
    });
  });

  it('handles processor restart successfully', async () => {
    const user = userEvent.setup();
    mockApiClient.restartProcessor.mockResolvedValue({ data: { success: true } });
    renderSettings();

    await waitFor(() => {
      expect(screen.getByText('Processor Control')).toBeInTheDocument();
    });

    const restartButton = screen.getByText('Restart');
    await user.click(restartButton);

    await waitFor(() => {
      expect(mockApiClient.restartProcessor).toHaveBeenCalled();
    });

    await waitFor(() => {
      expect(screen.getByText('Processor restarted successfully')).toBeInTheDocument();
    });
  });

  it('displays error message when restart fails', async () => {
    const user = userEvent.setup();
    mockApiClient.restartProcessor.mockRejectedValue(new Error('Restart failed'));
    renderSettings();

    await waitFor(() => {
      expect(screen.getByText('Processor Control')).toBeInTheDocument();
    });

    const restartButton = screen.getByText('Restart');
    await user.click(restartButton);

    await waitFor(() => {
      expect(screen.getByText('Failed to restart processor')).toBeInTheDocument();
    });
  });

  it('displays configuration notes', () => {
    renderSettings();

    expect(screen.getByText('Configuration Notes')).toBeInTheDocument();
    expect(screen.getByText(/Changes to processor state and test mode configuration take effect immediately/)).toBeInTheDocument();
  });

  it('displays health check items', async () => {
    renderSettings();

    await waitFor(() => {
      expect(screen.getByText('Database Connection')).toBeInTheDocument();
      expect(screen.getByText('Queue Service')).toBeInTheDocument();
      expect(screen.getByText('Console Application')).toBeInTheDocument();
    });
  });

  it('handles test mode toggle', async () => {
    const user = userEvent.setup();
    renderSettings();

    await waitFor(() => {
      expect(screen.getByText('Test Mode Configuration')).toBeInTheDocument();
    });

    const toggleSwitch = screen.getByRole('checkbox');
    await user.click(toggleSwitch);

    await waitFor(() => {
      expect(screen.getByText('Test mode enabled successfully')).toBeInTheDocument();
    });
  });
});
