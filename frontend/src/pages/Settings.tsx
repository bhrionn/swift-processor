import React, { useEffect, useState } from 'react';
import { apiClient } from '@/services/ApiClient';
import type { SystemStatus } from '@/types/Message';
import ProcessorControl from '@/components/ProcessorControl';
import TestModeConfig from '@/components/TestModeConfig';
import SystemHealthMonitor from '@/components/SystemHealthMonitor';

interface HealthCheckItem {
  name: string;
  status: 'healthy' | 'unhealthy' | 'degraded' | 'unknown';
  message?: string;
  lastChecked?: Date;
}

const Settings: React.FC = () => {
  const [systemStatus, setSystemStatus] = useState<SystemStatus | null>(null);
  const [loading, setLoading] = useState<boolean>(true);
  const [restarting, setRestarting] = useState<boolean>(false);
  const [testModeEnabled, setTestModeEnabled] = useState<boolean>(false);
  const [updatingTestMode, setUpdatingTestMode] = useState<boolean>(false);
  const [error, setError] = useState<string | null>(null);
  const [successMessage, setSuccessMessage] = useState<string | null>(null);

  // Mock health checks - will be replaced with actual API calls
  const [healthChecks] = useState<HealthCheckItem[]>([
    {
      name: 'Database Connection',
      status: 'healthy',
      message: 'Connected to SQLite database',
      lastChecked: new Date(),
    },
    {
      name: 'Queue Service',
      status: 'healthy',
      message: 'Local queue service operational',
      lastChecked: new Date(),
    },
    {
      name: 'Console Application',
      status: 'healthy',
      message: 'Console processor is running',
      lastChecked: new Date(),
    },
  ]);

  useEffect(() => {
    fetchSystemStatus();
  }, []);

  const fetchSystemStatus = async (): Promise<void> => {
    try {
      setLoading(true);
      const response = await apiClient.getSystemStatus();
      setSystemStatus(response.data);
      setError(null);
    } catch (err) {
      setError('Failed to load system status');
      console.error('Error fetching system status:', err);
    } finally {
      setLoading(false);
    }
  };

  const handleRestart = async (): Promise<void> => {
    try {
      setRestarting(true);
      setError(null);
      await apiClient.restartProcessor();
      setSuccessMessage('Processor restarted successfully');
      setTimeout(() => {
        setSuccessMessage(null);
        fetchSystemStatus();
      }, 2000);
    } catch (err) {
      setError('Failed to restart processor');
      console.error('Error restarting processor:', err);
    } finally {
      setRestarting(false);
    }
  };

  const handleStart = async (): Promise<void> => {
    try {
      setError(null);
      // API endpoint for starting processor would be called here
      setSuccessMessage('Processor started successfully');
      setTimeout(() => {
        setSuccessMessage(null);
        fetchSystemStatus();
      }, 2000);
    } catch (err) {
      setError('Failed to start processor');
      console.error('Error starting processor:', err);
    }
  };

  const handleStop = async (): Promise<void> => {
    try {
      setError(null);
      // API endpoint for stopping processor would be called here
      setSuccessMessage('Processor stopped successfully');
      setTimeout(() => {
        setSuccessMessage(null);
        fetchSystemStatus();
      }, 2000);
    } catch (err) {
      setError('Failed to stop processor');
      console.error('Error stopping processor:', err);
    }
  };

  const handleTestModeToggle = async (enabled: boolean): Promise<void> => {
    try {
      setUpdatingTestMode(true);
      setError(null);
      // API endpoint for toggling test mode would be called here
      setTestModeEnabled(enabled);
      setSuccessMessage(
        enabled ? 'Test mode enabled successfully' : 'Test mode disabled successfully'
      );
      setTimeout(() => {
        setSuccessMessage(null);
      }, 3000);
    } catch (err) {
      setError('Failed to update test mode');
      console.error('Error updating test mode:', err);
    } finally {
      setUpdatingTestMode(false);
    }
  };

  const handleRefreshHealth = (): void => {
    // Refresh health checks
    fetchSystemStatus();
  };

  return (
    <div className="space-y-6">
      <div>
        <h1 className="text-3xl font-bold text-gray-900">Settings</h1>
        <p className="mt-2 text-sm text-gray-600">
          Manage system configuration and monitor health
        </p>
      </div>

      {/* Success Message */}
      {successMessage && (
        <div className="bg-green-50 border border-green-200 text-green-700 px-4 py-3 rounded-md flex items-center">
          <svg className="w-5 h-5 mr-2" fill="currentColor" viewBox="0 0 20 20">
            <path
              fillRule="evenodd"
              d="M10 18a8 8 0 100-16 8 8 0 000 16zm3.707-9.293a1 1 0 00-1.414-1.414L9 10.586 7.707 9.293a1 1 0 00-1.414 1.414l2 2a1 1 0 001.414 0l4-4z"
              clipRule="evenodd"
            />
          </svg>
          {successMessage}
        </div>
      )}

      {/* Error Message */}
      {error && (
        <div className="bg-red-50 border border-red-200 text-red-700 px-4 py-3 rounded-md flex items-center">
          <svg className="w-5 h-5 mr-2" fill="currentColor" viewBox="0 0 20 20">
            <path
              fillRule="evenodd"
              d="M10 18a8 8 0 100-16 8 8 0 000 16zM8.707 7.293a1 1 0 00-1.414 1.414L8.586 10l-1.293 1.293a1 1 0 101.414 1.414L10 11.414l1.293 1.293a1 1 0 001.414-1.414L11.414 10l1.293-1.293a1 1 0 00-1.414-1.414L10 8.586 8.707 7.293z"
              clipRule="evenodd"
            />
          </svg>
          {error}
        </div>
      )}

      {/* Settings Grid */}
      <div className="grid grid-cols-1 lg:grid-cols-2 gap-6">
        {/* Processor Control */}
        <ProcessorControl
          status={systemStatus}
          loading={loading}
          onRestart={handleRestart}
          onStop={handleStop}
          onStart={handleStart}
          restarting={restarting}
        />

        {/* Test Mode Configuration */}
        <TestModeConfig
          enabled={testModeEnabled}
          onToggle={handleTestModeToggle}
          updating={updatingTestMode}
        />
      </div>

      {/* System Health Monitor */}
      <SystemHealthMonitor
        healthChecks={healthChecks}
        loading={loading}
        onRefresh={handleRefreshHealth}
      />

      {/* Additional Information */}
      <div className="bg-blue-50 border border-blue-200 rounded-lg p-4">
        <div className="flex">
          <svg
            className="w-5 h-5 text-blue-600 mr-2 flex-shrink-0"
            fill="none"
            stroke="currentColor"
            viewBox="0 0 24 24"
          >
            <path
              strokeLinecap="round"
              strokeLinejoin="round"
              strokeWidth={2}
              d="M13 16h-1v-4h-1m1-4h.01M21 12a9 9 0 11-18 0 9 9 0 0118 0z"
            />
          </svg>
          <div className="flex-1">
            <h3 className="text-sm font-medium text-blue-900">Configuration Notes</h3>
            <p className="text-sm text-blue-700 mt-1">
              Changes to processor state and test mode configuration take effect immediately. The
              system will automatically reconnect to services as needed.
            </p>
          </div>
        </div>
      </div>
    </div>
  );
};

export default Settings;
