import React from 'react';

interface HealthCheckItem {
  name: string;
  status: 'healthy' | 'unhealthy' | 'degraded' | 'unknown';
  message?: string;
  lastChecked?: Date;
}

interface SystemHealthMonitorProps {
  healthChecks: HealthCheckItem[];
  loading: boolean;
  onRefresh: () => void;
}

const SystemHealthMonitor: React.FC<SystemHealthMonitorProps> = ({
  healthChecks,
  loading,
  onRefresh,
}) => {
  const getStatusIcon = (status: string): React.ReactElement => {
    switch (status) {
      case 'healthy':
        return (
          <svg className="w-5 h-5 text-green-600" fill="currentColor" viewBox="0 0 20 20">
            <path
              fillRule="evenodd"
              d="M10 18a8 8 0 100-16 8 8 0 000 16zm3.707-9.293a1 1 0 00-1.414-1.414L9 10.586 7.707 9.293a1 1 0 00-1.414 1.414l2 2a1 1 0 001.414 0l4-4z"
              clipRule="evenodd"
            />
          </svg>
        );
      case 'unhealthy':
        return (
          <svg className="w-5 h-5 text-red-600" fill="currentColor" viewBox="0 0 20 20">
            <path
              fillRule="evenodd"
              d="M10 18a8 8 0 100-16 8 8 0 000 16zM8.707 7.293a1 1 0 00-1.414 1.414L8.586 10l-1.293 1.293a1 1 0 101.414 1.414L10 11.414l1.293 1.293a1 1 0 001.414-1.414L11.414 10l1.293-1.293a1 1 0 00-1.414-1.414L10 8.586 8.707 7.293z"
              clipRule="evenodd"
            />
          </svg>
        );
      case 'degraded':
        return (
          <svg className="w-5 h-5 text-yellow-600" fill="currentColor" viewBox="0 0 20 20">
            <path
              fillRule="evenodd"
              d="M8.257 3.099c.765-1.36 2.722-1.36 3.486 0l5.58 9.92c.75 1.334-.213 2.98-1.742 2.98H4.42c-1.53 0-2.493-1.646-1.743-2.98l5.58-9.92zM11 13a1 1 0 11-2 0 1 1 0 012 0zm-1-8a1 1 0 00-1 1v3a1 1 0 002 0V6a1 1 0 00-1-1z"
              clipRule="evenodd"
            />
          </svg>
        );
      default:
        return (
          <svg className="w-5 h-5 text-gray-400" fill="currentColor" viewBox="0 0 20 20">
            <path
              fillRule="evenodd"
              d="M18 10a8 8 0 11-16 0 8 8 0 0116 0zm-7-4a1 1 0 11-2 0 1 1 0 012 0zM9 9a1 1 0 000 2v3a1 1 0 001 1h1a1 1 0 100-2v-3a1 1 0 00-1-1H9z"
              clipRule="evenodd"
            />
          </svg>
        );
    }
  };

  const getStatusColor = (status: string): string => {
    switch (status) {
      case 'healthy':
        return 'bg-green-50 border-green-200';
      case 'unhealthy':
        return 'bg-red-50 border-red-200';
      case 'degraded':
        return 'bg-yellow-50 border-yellow-200';
      default:
        return 'bg-gray-50 border-gray-200';
    }
  };

  const getOverallStatus = (): { status: string; color: string } => {
    if (healthChecks.some((check) => check.status === 'unhealthy')) {
      return { status: 'Unhealthy', color: 'text-red-600' };
    }
    if (healthChecks.some((check) => check.status === 'degraded')) {
      return { status: 'Degraded', color: 'text-yellow-600' };
    }
    if (healthChecks.every((check) => check.status === 'healthy')) {
      return { status: 'Healthy', color: 'text-green-600' };
    }
    return { status: 'Unknown', color: 'text-gray-600' };
  };

  const overall = getOverallStatus();

  return (
    <div className="bg-white rounded-lg shadow p-6">
      <div className="flex items-center justify-between mb-4">
        <h2 className="text-xl font-semibold text-gray-900">System Health</h2>
        <button
          onClick={onRefresh}
          disabled={loading}
          className="px-3 py-1 text-sm text-blue-600 hover:text-blue-700 disabled:text-gray-400 transition-colors"
        >
          <svg
            className={`w-5 h-5 ${loading ? 'animate-spin' : ''}`}
            fill="none"
            stroke="currentColor"
            viewBox="0 0 24 24"
          >
            <path
              strokeLinecap="round"
              strokeLinejoin="round"
              strokeWidth={2}
              d="M4 4v5h.582m15.356 2A8.001 8.001 0 004.582 9m0 0H9m11 11v-5h-.581m0 0a8.003 8.003 0 01-15.357-2m15.357 2H15"
            />
          </svg>
        </button>
      </div>

      {/* Overall Status */}
      <div className="mb-4 p-4 bg-gray-50 rounded-lg">
        <div className="flex items-center justify-between">
          <span className="text-sm font-medium text-gray-600">Overall Status</span>
          <span className={`text-sm font-semibold ${overall.color}`}>{overall.status}</span>
        </div>
      </div>

      {/* Health Checks */}
      <div className="space-y-3">
        {healthChecks.length === 0 ? (
          <div className="text-center py-8 text-gray-500">
            <p>No health checks available</p>
          </div>
        ) : (
          healthChecks.map((check, index) => (
            <div
              key={index}
              className={`border rounded-lg p-4 ${getStatusColor(check.status)}`}
            >
              <div className="flex items-start">
                <div className="flex-shrink-0">{getStatusIcon(check.status)}</div>
                <div className="ml-3 flex-1">
                  <h3 className="text-sm font-medium text-gray-900">{check.name}</h3>
                  {check.message && (
                    <p className="text-sm text-gray-600 mt-1">{check.message}</p>
                  )}
                  {check.lastChecked && (
                    <p className="text-xs text-gray-500 mt-1">
                      Last checked: {new Date(check.lastChecked).toLocaleString()}
                    </p>
                  )}
                </div>
              </div>
            </div>
          ))
        )}
      </div>
    </div>
  );
};

export default SystemHealthMonitor;
