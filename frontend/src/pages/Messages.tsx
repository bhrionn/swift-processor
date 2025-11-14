import React, { useEffect, useState } from 'react';
import { apiClient } from '@/services/ApiClient';
import type { Message, MessageFilter, MessageStatus } from '@/types/Message';
import { useDebounce } from '@/hooks/useDebounce';
import MessageFilters from '@/components/MessageFilters';
import MessageTable from '@/components/MessageTable';
import MessageDetailModal from '@/components/MessageDetailModal';
import Pagination from '@/components/Pagination';

const Messages: React.FC = () => {
  const [messages, setMessages] = useState<Message[]>([]);
  const [totalCount, setTotalCount] = useState<number>(0);
  const [loading, setLoading] = useState<boolean>(false);
  const [error, setError] = useState<string | null>(null);
  const [selectedMessage, setSelectedMessage] = useState<Message | null>(null);
  const [isModalOpen, setIsModalOpen] = useState<boolean>(false);

  // Filter state
  const [currentPage, setCurrentPage] = useState<number>(1);
  const [itemsPerPage] = useState<number>(20);
  const [status, setStatus] = useState<number | undefined>(undefined);
  const [fromDate, setFromDate] = useState<string>('');
  const [toDate, setToDate] = useState<string>('');
  const [searchTerm, setSearchTerm] = useState<string>('');

  // Debounce search term
  const debouncedSearchTerm = useDebounce(searchTerm, 500);

  // Fetch messages when filters change
  useEffect(() => {
    fetchMessages();
  }, [currentPage, status, fromDate, toDate, debouncedSearchTerm]);

  const fetchMessages = async (): Promise<void> => {
    try {
      setLoading(true);
      setError(null);

      const filter: MessageFilter = {
        skip: (currentPage - 1) * itemsPerPage,
        take: itemsPerPage,
        ...(status !== undefined && { status: status as MessageStatus }),
        ...(fromDate && { fromDate: new Date(fromDate) }),
        ...(toDate && { toDate: new Date(toDate) }),
      };

      const response = await apiClient.getMessages(filter);
      setMessages(response.data.items);
      setTotalCount(response.data.totalCount);
    } catch (err) {
      setError('Failed to load messages');
      console.error('Error fetching messages:', err);
    } finally {
      setLoading(false);
    }
  };

  const handleMessageClick = (message: Message): void => {
    setSelectedMessage(message);
    setIsModalOpen(true);
  };

  const handleCloseModal = (): void => {
    setIsModalOpen(false);
    setSelectedMessage(null);
  };

  const handlePageChange = (page: number): void => {
    setCurrentPage(page);
  };

  const handleClearFilters = (): void => {
    setStatus(undefined);
    setFromDate('');
    setToDate('');
    setSearchTerm('');
    setCurrentPage(1);
  };

  return (
    <div className="space-y-6">
      <div>
        <h1 className="text-3xl font-bold text-gray-900">Messages</h1>
        <p className="mt-2 text-sm text-gray-600">
          View and search processed SWIFT messages
        </p>
      </div>

      {error && (
        <div className="bg-red-50 border border-red-200 text-red-700 px-4 py-3 rounded-md">
          {error}
        </div>
      )}

      {/* Filters */}
      <MessageFilters
        status={status}
        fromDate={fromDate}
        toDate={toDate}
        searchTerm={searchTerm}
        onStatusChange={setStatus}
        onFromDateChange={setFromDate}
        onToDateChange={setToDate}
        onSearchChange={setSearchTerm}
        onClearFilters={handleClearFilters}
      />

      {/* Message Table */}
      <MessageTable
        messages={messages}
        loading={loading}
        onMessageClick={handleMessageClick}
      />

      {/* Pagination */}
      {!loading && messages.length > 0 && (
        <Pagination
          currentPage={currentPage}
          totalItems={totalCount}
          itemsPerPage={itemsPerPage}
          onPageChange={handlePageChange}
        />
      )}

      {/* Message Detail Modal */}
      <MessageDetailModal
        message={selectedMessage}
        isOpen={isModalOpen}
        onClose={handleCloseModal}
      />
    </div>
  );
};

export default Messages;
