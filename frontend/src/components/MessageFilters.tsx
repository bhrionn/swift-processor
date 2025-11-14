import React from 'react';
import { MessageStatus } from '@/types/Message';

interface MessageFiltersProps {
  status: number | undefined;
  fromDate: string;
  toDate: string;
  searchTerm: string;
  onStatusChange: (status: number | undefined) => void;
  onFromDateChange: (date: string) => void;
  onToDateChange: (date: string) => void;
  onSearchChange: (term: string) => void;
  onClearFilters: () => void;
}

const MessageFilters: React.FC<MessageFiltersProps> = ({
  status,
  fromDate,
  toDate,
  searchTerm,
  onStatusChange,
  onFromDateChange,
  onToDateChange,
  onSearchChange,
  onClearFilters,
}) => {
  const getStatusLabel = (statusValue: number): string => {
    switch (statusValue) {
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
    <div className="bg-white rounded-lg shadow p-6 mb-6">
      <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-4 gap-4">
        {/* Search */}
        <div>
          <label htmlFor="search" className="block text-sm font-medium text-gray-700 mb-1">
            Search
          </label>
          <input
            type="text"
            id="search"
            value={searchTerm}
            onChange={(e) => onSearchChange(e.target.value)}
            placeholder="Search messages..."
            className="w-full px-3 py-2 border border-gray-300 rounded-md focus:outline-none focus:ring-2 focus:ring-blue-500"
          />
        </div>

        {/* Status Filter */}
        <div>
          <label htmlFor="status" className="block text-sm font-medium text-gray-700 mb-1">
            Status
          </label>
          <select
            id="status"
            value={status ?? ''}
            onChange={(e) => onStatusChange(e.target.value ? Number(e.target.value) : undefined)}
            className="w-full px-3 py-2 border border-gray-300 rounded-md focus:outline-none focus:ring-2 focus:ring-blue-500"
          >
            <option value="">All Statuses</option>
            {Object.values(MessageStatus)
              .filter((v) => typeof v === 'number')
              .map((statusValue) => (
                <option key={statusValue} value={statusValue}>
                  {getStatusLabel(statusValue as number)}
                </option>
              ))}
          </select>
        </div>

        {/* From Date */}
        <div>
          <label htmlFor="fromDate" className="block text-sm font-medium text-gray-700 mb-1">
            From Date
          </label>
          <input
            type="date"
            id="fromDate"
            value={fromDate}
            onChange={(e) => onFromDateChange(e.target.value)}
            className="w-full px-3 py-2 border border-gray-300 rounded-md focus:outline-none focus:ring-2 focus:ring-blue-500"
          />
        </div>

        {/* To Date */}
        <div>
          <label htmlFor="toDate" className="block text-sm font-medium text-gray-700 mb-1">
            To Date
          </label>
          <input
            type="date"
            id="toDate"
            value={toDate}
            onChange={(e) => onToDateChange(e.target.value)}
            className="w-full px-3 py-2 border border-gray-300 rounded-md focus:outline-none focus:ring-2 focus:ring-blue-500"
          />
        </div>
      </div>

      {/* Clear Filters Button */}
      <div className="mt-4">
        <button
          onClick={onClearFilters}
          className="px-4 py-2 text-sm text-gray-700 bg-gray-100 rounded-md hover:bg-gray-200 transition-colors"
        >
          Clear Filters
        </button>
      </div>
    </div>
  );
};

export default MessageFilters;
