import { useEffect, useState } from 'react';
import { HubConnection, HubConnectionBuilder } from '@microsoft/signalr';
// import type { Message, SystemStatus } from '@/types/Message';

// Interface for SignalR hub methods - will be used in later tasks
// interface MessageHub {
//   ReceiveMessage: (message: Message) => void;
//   ReceiveSystemStatus: (status: SystemStatus) => void;
// }

export const useSignalR = (): HubConnection | null => {
  const [connection, setConnection] = useState<HubConnection | null>(null);

  useEffect(() => {
    const hubUrl = import.meta.env.VITE_SIGNALR_URL || 'http://localhost:5000/messageHub';
    
    const newConnection = new HubConnectionBuilder()
      .withUrl(hubUrl)
      .build();

    setConnection(newConnection);

    return () => {
      newConnection.stop();
    };
  }, []);

  return connection;
};