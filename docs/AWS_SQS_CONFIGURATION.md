# AWS SQS Configuration Guide

## Overview

The SWIFT Message Processor supports AWS SQS (Simple Queue Service) for production and staging environments. This guide explains how to configure and use AWS SQS with the application.

## Prerequisites

1. AWS Account with SQS access
2. AWS credentials configured (via AWS CLI, environment variables, or IAM roles)
3. SQS queues created in your AWS account

## Creating SQS Queues

You need to create three queues in AWS SQS:

1. **Input Queue**: Receives incoming SWIFT messages for processing
2. **Completed Queue**: Stores successfully processed messages
3. **Dead Letter Queue**: Stores failed messages for troubleshooting

### Using AWS CLI

```bash
# Set your AWS region
export AWS_REGION=us-east-1

# Create input queue
aws sqs create-queue --queue-name swift-input-messages

# Create completed queue
aws sqs create-queue --queue-name swift-completed-messages

# Create dead letter queue
aws sqs create-queue --queue-name swift-failed-messages
```

### Using AWS Console

1. Navigate to AWS SQS Console
2. Click "Create queue"
3. Choose "Standard" queue type
4. Enter queue name (e.g., `swift-input-messages`)
5. Configure settings as needed
6. Click "Create queue"
7. Repeat for completed and dead letter queues

## Configuration

### Application Settings

Update your `appsettings.json` or environment-specific configuration file:

```json
{
  "Queue": {
    "Provider": "AmazonSQS",
    "Region": "us-east-1",
    "Settings": {
      "InputQueue": "swift-input-messages",
      "CompletedQueue": "swift-completed-messages",
      "DeadLetterQueue": "swift-failed-messages"
    }
  },
  "AWS": {
    "Region": "us-east-1"
  }
}
```

### AWS Credentials

The application uses the AWS SDK for .NET, which supports multiple credential sources:

#### 1. Environment Variables

```bash
export AWS_ACCESS_KEY_ID=your_access_key
export AWS_SECRET_ACCESS_KEY=your_secret_key
export AWS_REGION=us-east-1
```

#### 2. AWS Credentials File

Create or update `~/.aws/credentials`:

```ini
[default]
aws_access_key_id = your_access_key
aws_secret_access_key = your_secret_key
```

And `~/.aws/config`:

```ini
[default]
region = us-east-1
```

#### 3. IAM Roles (Recommended for Production)

When running on AWS infrastructure (EC2, ECS, Lambda), use IAM roles:

1. Create an IAM role with SQS permissions
2. Attach the role to your compute resource
3. No credentials needed in configuration

### Required IAM Permissions

The application requires the following SQS permissions:

```json
{
  "Version": "2012-10-17",
  "Statement": [
    {
      "Effect": "Allow",
      "Action": [
        "sqs:SendMessage",
        "sqs:ReceiveMessage",
        "sqs:DeleteMessage",
        "sqs:GetQueueUrl",
        "sqs:GetQueueAttributes"
      ],
      "Resource": [
        "arn:aws:sqs:us-east-1:123456789012:swift-input-messages",
        "arn:aws:sqs:us-east-1:123456789012:swift-completed-messages",
        "arn:aws:sqs:us-east-1:123456789012:swift-failed-messages"
      ]
    }
  ]
}
```

## Environment-Specific Configuration

### Development (Local)

Use in-memory queues for local development:

```json
{
  "Queue": {
    "Provider": "InMemory"
  }
}
```

### Staging

Use AWS SQS with staging-specific queue names:

```json
{
  "Queue": {
    "Provider": "AmazonSQS",
    "Region": "us-east-1",
    "Settings": {
      "InputQueue": "swift-staging-input-messages",
      "CompletedQueue": "swift-staging-completed-messages",
      "DeadLetterQueue": "swift-staging-failed-messages"
    }
  }
}
```

### Production

Use AWS SQS with production queue names:

```json
{
  "Queue": {
    "Provider": "AmazonSQS",
    "Region": "us-east-1",
    "Settings": {
      "InputQueue": "swift-input-messages",
      "CompletedQueue": "swift-completed-messages",
      "DeadLetterQueue": "swift-failed-messages"
    }
  }
}
```

## Features

### Long Polling

The AmazonSQSService uses long polling (5 seconds) to reduce costs and improve efficiency:

```csharp
var request = new ReceiveMessageRequest
{
    QueueUrl = queueUrl,
    MaxNumberOfMessages = 1,
    WaitTimeSeconds = 5  // Long polling
};
```

### Automatic Message Deletion

Messages are automatically deleted from the queue after successful retrieval to prevent duplicate processing.

### Health Checks

The service implements health checks that verify SQS connectivity:

```csharp
public async Task<bool> IsHealthyAsync()
{
    // Checks queue attributes to verify connectivity
}
```

### Queue Statistics

Get real-time statistics about queue depth and message counts:

```csharp
var stats = await queueService.GetStatisticsAsync();
Console.WriteLine($"Messages in queue: {stats.MessagesInQueue}");
```

## Monitoring

### CloudWatch Metrics

AWS SQS automatically publishes metrics to CloudWatch:

- `ApproximateNumberOfMessagesVisible`: Messages available for retrieval
- `ApproximateNumberOfMessagesNotVisible`: Messages in flight
- `NumberOfMessagesSent`: Total messages sent
- `NumberOfMessagesReceived`: Total messages received
- `NumberOfMessagesDeleted`: Total messages deleted

### Application Logging

The AmazonSQSService logs important events:

- Message send/receive operations
- Queue URL resolution
- Health check results
- Error conditions

## Troubleshooting

### Queue Not Found

**Error**: `QueueDoesNotExistException`

**Solution**: Verify queue names in configuration match actual queue names in AWS.

### Access Denied

**Error**: `AccessDeniedException`

**Solution**: Check IAM permissions and ensure credentials have required SQS permissions.

### Connection Timeout

**Error**: Connection timeout or network errors

**Solution**: 
- Verify network connectivity to AWS
- Check security group rules
- Verify AWS region configuration

### Messages Not Processing

**Possible Causes**:
- Queue visibility timeout too short
- Application not polling frequently enough
- Messages stuck in dead letter queue

**Solution**:
- Check CloudWatch metrics
- Review application logs
- Inspect dead letter queue for failed messages

## Cost Optimization

1. **Use Long Polling**: Reduces empty receives and costs
2. **Batch Operations**: Send/receive multiple messages when possible
3. **Message Retention**: Configure appropriate retention periods
4. **Dead Letter Queues**: Prevent infinite retries

## Best Practices

1. **Use IAM Roles**: Avoid hardcoding credentials
2. **Enable Encryption**: Use SSE-SQS or SSE-KMS for sensitive data
3. **Monitor Metrics**: Set up CloudWatch alarms for queue depth
4. **Configure DLQ**: Set up dead letter queues with appropriate retry policies
5. **Test Locally**: Use LocalQueueService for development
6. **Tag Resources**: Tag queues for cost allocation and management

## Migration from Local to AWS SQS

To migrate from local development to AWS SQS:

1. Create SQS queues in AWS
2. Update configuration to use `AmazonSQS` provider
3. Configure AWS credentials
4. Test connectivity with health checks
5. Monitor CloudWatch metrics
6. Gradually increase traffic

## Additional Resources

- [AWS SQS Documentation](https://docs.aws.amazon.com/sqs/)
- [AWS SDK for .NET](https://aws.amazon.com/sdk-for-net/)
- [SQS Best Practices](https://docs.aws.amazon.com/AWSSimpleQueueService/latest/SQSDeveloperGuide/sqs-best-practices.html)
