import React from 'react';
import type { Message } from '@/types/Message';
import { MessageStatus } from '@/types/Message';

interface MessageDetailModalProps {
  message: Message | null;
  isOpen: boolean;
  onClose: () => void;
}

const MessageDetailModal: React.FC<MessageDetailModalProps> = ({ message, isOpen, onClose }) => {
  if (!isOpen || !message) return null;

  const getStatusLabel = (status: number): string => {
    switch (status) {
      case MessageStatus.Pending:
        return 'Pending';
      case MessageStatus.Processing:
        return 'Processing';
      case MessageStatus.Processed:
        return 'Processed';
      case MessageStatus.Failed:
        return 'Failed';
      case MessageStatus.DeadLetter:
        return 'Dead Letter';
      default:
        return 'Unknown';
    }
  };

  return (
    <div className="fixed inset-0 z-50 overflow-y-auto">
      <div className="flex items-center justify-center min-h-screen px-4 pt-4 pb-20 text-center sm:block sm:p-0">
        {/* Background overlay */}
        <div
          className="fixed inset-0 transition-opacity bg-gray-500 bg-opacity-75"
          onClick={onClose}
        ></div>

        {/* Modal panel */}
        <div className="inline-block align-bottom bg-white rounded-lg text-left overflow-hidden shadow-xl transform transition-all sm:my-8 sm:align-middle sm:max-w-3xl sm:w-full">
          {/* Header */}
          <div className="bg-gray-50 px-6 py-4 border-b border-gray-200">
            <div className="flex items-center justify-between">
              <h3 className="text-lg font-semibold text-gray-900">Message Details</h3>
              <button
                onClick={onClose}
                className="text-gray-400 hover:text-gray-500 focus:outline-none"
              >
                <svg className="h-6 w-6" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                  <path
                    strokeLinecap="round"
                    strokeLinejoin="round"
                    strokeWidth={2}
                    d="M6 18L18 6M6 6l12 12"
                  />
                </svg>
              </button>
            </div>
          </div>

          {/* Content */}
          <div className="px-6 py-4 max-h-96 overflow-y-auto">
            <div className="space-y-4">
              {/* Message ID */}
              <div>
                <label className="block text-sm font-medium text-gray-700 mb-1">Message ID</label>
                <p className="text-sm text-gray-900 font-mono bg-gray-50 p-2 rounded">
                  {message.id}
                </p>
              </div>

              {/* Message Type */}
              <div>
                <label className="block text-sm font-medium text-gray-700 mb-1">Message Type</label>
                <p className="text-sm text-gray-900">{message.messageType}</p>
              </div>

              {/* Status */}
              <div>
                <label className="block text-sm font-medium text-gray-700 mb-1">Status</label>
                <p className="text-sm text-gray-900">{getStatusLabel(message.status)}</p>
              </div>

              {/* Processed At */}
              <div>
                <label className="block text-sm font-medium text-gray-700 mb-1">Processed At</label>
                <p className="text-sm text-gray-900">
                  {new Date(message.processedAt).toLocaleString()}
                </p>
              </div>

              {/* Error Details */}
              {message.errorDetails && (
                <div>
                  <label className="block text-sm font-medium text-gray-700 mb-1">
                    Error Details
                  </label>
                  <p className="text-sm text-red-600 bg-red-50 p-3 rounded">
                    {message.errorDetails}
                  </p>
                </div>
              )}

              {/* Raw Message - Placeholder */}
              <div>
                <label className="block text-sm font-medium text-gray-700 mb-1">
                  Raw Message Content
                </label>
                <div className="text-sm text-gray-500 bg-gray-50 p-3 rounded font-mono text-xs overflow-x-auto">
                  <p>Raw message content would be displayed here</p>
                  <p className="mt-2 text-xs">
                    (Full message parsing and field display will be available when integrated with
                    the backend API)
                  </p>
                </div>
              </div>
            </div>
          </div>

          {/* Footer */}
          <div className="bg-gray-50 px-6 py-4 border-t border-gray-200">
            <button
              onClick={onClose}
              className="w-full sm:w-auto px-4 py-2 bg-blue-600 text-white rounded-md hover:bg-blue-700 transition-colors"
            >
              Close
            </button>
          </div>
        </div>
      </div>
    </div>
  );
};

export default MessageDetailModal;
