import React, { useEffect, useState } from 'react';
import { apiClient } from '@/services/ApiClient';
import { useSignalR } from '@/hooks/useSignalR';
import type { SystemStatus } from '@/types/Message';
import StatCard from '@/components/StatCard';
import SystemStatusCard from '@/components/SystemStatusCard';

const Dashboard: React.FC = () => {
  const [systemStatus, setSystemStatus] = useState<SystemStatus | null>(null);
  const [loading, setLoading] = useState<boolean>(true);
  const [restarting, setRestarting] = useState<boolean>(false);
  const [error, setError] = useState<string | null>(null);
  const connection = useSignalR();

  // Fetch initial system status
  useEffect(() => {
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

    fetchSystemStatus();
  }, []);

  // Set up SignalR connection for real-time updates
  useEffect(() => {
    if (!connection) return;

    const startConnection = async (): Promise<void> => {
      try {
        await connection.start();
        console.log('SignalR connected');

        // Listen for system status updates
        connection.on('ReceiveSystemStatus', (status: SystemStatus) => {
          console.log('Received system status update:', status);
          setSystemStatus(status);
        });

        // Listen for message processing updates
        connection.on('ReceiveMessage', (message: unknown) => {
          console.log('Received message update:', message);
          // Refresh system status when new messages are processed
          fetchSystemStatusSilently();
        });
      } catch (err) {
        console.error('Error connecting to SignalR:', err);
      }
    };

    startConnection();

    return () => {
      if (connection) {
        connection.stop();
      }
    };
  }, [connection]);

  const fetchSystemStatusSilently = async (): Promise<void> => {
    try {
      const response = await apiClient.getSystemStatus();
      setSystemStatus(response.data);
    } catch (err) {
      console.error('Error fetching system status:', err);
    }
  };

  const handleRestart = async (): Promise<void> => {
    try {
      setRestarting(true);
      await apiClient.restartProcessor();
      // Wait a moment for the processor to restart
      setTimeout(async () => {
        await fetchSystemStatusSilently();
        setRestarting(false);
      }, 2000);
    } catch (err) {
      console.error('Error restarting processor:', err);
      setError('Failed to restart processor');
      setRestarting(false);
    }
  };

  return (
    <div className="space-y-6">
      <div>
        <h1 className="text-3xl font-bold text-gray-900">Dashboard</h1>
        <p className="mt-2 text-sm text-gray-600">
          Monitor SWIFT message processing and system status
        </p>
      </div>

      {error && (
        <div className="bg-red-50 border border-red-200 text-red-700 px-4 py-3 rounded-md">
          {error}
        </div>
      )}

      {/* Statistics Grid */}
      <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-4 gap-6">
        <StatCard
          title="Messages Processed"
          value={systemStatus?.messagesProcessed ?? 0}
          color="green"
          icon={
            <svg className="w-6 h-6" fill="none" stroke="currentColor" viewBox="0 0 24 24">
              <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M9 12l2 2 4-4m6 2a9 9 0 11-18 0 9 9 0 0118 0z" />
            </svg>
          }
        />
        
        <StatCard
          title="Messages Failed"
          value={systemStatus?.messagesFailed ?? 0}
          color="red"
          icon={
            <svg className="w-6 h-6" fill="none" stroke="currentColor" viewBox="0 0 24 24">
              <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M12 8v4m0 4h.01M21 12a9 9 0 11-18 0 9 9 0 0118 0z" />
            </svg>
          }
        />
        
        <StatCard
          title="Pending Messages"
          value={0}
          color="yellow"
          icon={
            <svg className="w-6 h-6" fill="none" stroke="currentColor" viewBox="0 0 24 24">
              <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M12 8v4l3 3m6-3a9 9 0 11-18 0 9 9 0 0118 0z" />
            </svg>
          }
        />
        
        <StatCard
          title="System Status"
          value={systemStatus?.status ?? 'Unknown'}
          color="blue"
          icon={
            <svg className="w-6 h-6" fill="none" stroke="currentColor" viewBox="0 0 24 24">
              <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M9 12h6m-6 4h6m2 5H7a2 2 0 01-2-2V5a2 2 0 012-2h5.586a1 1 0 01.707.293l5.414 5.414a1 1 0 01.293.707V19a2 2 0 01-2 2z" />
            </svg>
          }
        />
      </div>

      {/* System Status Card */}
      <div className="grid grid-cols-1 lg:grid-cols-3 gap-6">
        <div className="lg:col-span-1">
          <SystemStatusCard
            status={systemStatus}
            loading={loading}
            onRestart={handleRestart}
            restarting={restarting}
          />
        </div>
        
        {/* Recent Activity Placeholder */}
        <div className="lg:col-span-2">
          <div className="bg-white rounded-lg shadow p-6">
            <h2 className="text-xl font-semibold text-gray-900 mb-4">Recent Activity</h2>
            <div className="text-center py-8 text-gray-500">
              <p>Real-time message processing activity will appear here</p>
              <p className="text-sm mt-2">Connected to SignalR: {connection ? 'Yes' : 'No'}</p>
            </div>
          </div>
        </div>
      </div>
    </div>
  );
};

export default Dashboard;
