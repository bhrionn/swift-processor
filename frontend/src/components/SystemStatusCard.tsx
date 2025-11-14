import React from 'react';
import type { SystemStatus } from '@/types/Message';

interface SystemStatusCardProps {
  status: SystemStatus | null;
  loading: boolean;
  onRestart: () => void;
  restarting: boolean;
}

const SystemStatusCard: React.FC<SystemStatusCardProps> = ({
  status,
  loading,
  onRestart,
  restarting,
}) => {
  const getStatusColor = (isProcessing: boolean): string => {
    return isProcessing ? 'text-green-600' : 'text-gray-600';
  };

  const getStatusText = (isProcessing: boolean): string => {
    return isProcessing ? 'Running' : 'Stopped';
  };

  return (
    <div className="bg-white rounded-lg shadow p-6">
      <h2 className="text-xl font-semibold text-gray-900 mb-4">System Status</h2>
      
      {loading ? (
        <div className="flex items-center justify-center py-8">
          <div className="animate-spin rounded-full h-8 w-8 border-b-2 border-blue-600"></div>
        </div>
      ) : status ? (
        <div className="space-y-4">
          <div className="flex items-center justify-between">
            <span className="text-sm font-medium text-gray-600">Processor Status</span>
            <span className={`text-sm font-semibold ${getStatusColor(status.isProcessing)}`}>
              {getStatusText(status.isProcessing)}
            </span>
          </div>
          
          <div className="flex items-center justify-between">
            <span className="text-sm font-medium text-gray-600">Last Processed</span>
            <span className="text-sm text-gray-900">
              {status.lastProcessedAt 
                ? new Date(status.lastProcessedAt).toLocaleString()
                : 'Never'}
            </span>
          </div>

          <div className="pt-4 border-t border-gray-200">
            <button
              onClick={onRestart}
              disabled={restarting}
              className="w-full px-4 py-2 bg-blue-600 text-white rounded-md hover:bg-blue-700 disabled:bg-gray-400 disabled:cursor-not-allowed transition-colors"
            >
              {restarting ? 'Restarting...' : 'Restart Processor'}
            </button>
          </div>
        </div>
      ) : (
        <div className="text-center py-8 text-gray-500">
          Unable to load system status
        </div>
      )}
    </div>
  );
};

export default SystemStatusCard;
