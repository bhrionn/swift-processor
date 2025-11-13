namespace SwiftMessageProcessor.Core.Interfaces;

public interface IQueueServiceFactory
{
    IQueueService CreateQueueService(string provider);
}