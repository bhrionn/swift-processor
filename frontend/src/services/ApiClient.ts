import axios, { type AxiosResponse } from 'axios';
import type { Message, MessageFilter, PagedResult, SystemStatus } from '@/types/Message';

interface ApiResponse<T> {
    data: T;
    status: number;
    message?: string;
}

class MessageApiClient {
    private readonly baseUrl: string;

    constructor() {
        this.baseUrl = import.meta.env.VITE_API_URL || 'http://localhost:5000';
    }

    async getMessages(filter: MessageFilter): Promise<ApiResponse<PagedResult<Message>>> {
        try {
            const response: AxiosResponse<PagedResult<Message>> = await axios.get(
                `${this.baseUrl}/api/messages`,
                { params: filter }
            );

            return {
                data: response.data,
                status: response.status
            };
        } catch (error) {
            throw new Error(`Failed to fetch messages: ${error}`);
        }
    }

    async getSystemStatus(): Promise<ApiResponse<SystemStatus>> {
        try {
            const response: AxiosResponse<SystemStatus> = await axios.get(
                `${this.baseUrl}/api/system/status`
            );

            return {
                data: response.data,
                status: response.status
            };
        } catch (error) {
            throw new Error(`Failed to fetch system status: ${error}`);
        }
    }

    async restartProcessor(): Promise<ApiResponse<{ message: string }>> {
        try {
            const response: AxiosResponse<{ message: string }> = await axios.post(
                `${this.baseUrl}/api/system/restart`
            );

            return {
                data: response.data,
                status: response.status
            };
        } catch (error) {
            throw new Error(`Failed to restart processor: ${error}`);
        }
    }
}

export const apiClient = new MessageApiClient();