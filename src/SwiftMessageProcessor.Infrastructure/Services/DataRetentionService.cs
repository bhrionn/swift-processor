using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using SwiftMessageProcessor.Core.Models;
using SwiftMessageProcessor.Infrastructure.Data;

namespace SwiftMessageProcessor.Infrastructure.Services;

/// <summary>
/// Service for managing data retention and archiving policies
/// </summary>
public interface IDataRetentionService
{
    Task<int> ArchiveOldMessagesAsync(int retentionDays);
    Task<int> DeleteArchivedMessagesAsync(int archiveRetentionDays);
    Task<DataRetentionReport> GetRetentionReportAsync();
}

public class DataRetentionService : IDataRetentionService
{
    private readonly SwiftMessageContext _context;
    private readonly ILogger<DataRetentionService> _logger;
    private readonly int _defaultRetentionDays;
    private readonly int _defaultArchiveRetentionDays;

    public DataRetentionService(
        SwiftMessageContext context,
        IConfiguration configuration,
        ILogger<DataRetentionService> logger)
    {
        _context = context;
        _logger = logger;
        _defaultRetentionDays = configuration.GetValue<int>("DataRetention:RetentionDays", 365);
        _defaultArchiveRetentionDays = configuration.GetValue<int>("DataRetention:ArchiveRetentionDays", 2555); // 7 years
    }

    /// <summary>
    /// Archives messages older than the specified retention period
    /// </summary>
    public async Task<int> ArchiveOldMessagesAsync(int retentionDays = 0)
    {
        var days = retentionDays > 0 ? retentionDays : _defaultRetentionDays;
        var cutoffDate = DateTime.UtcNow.AddDays(-days);

        try
        {
            _logger.LogInformation("Starting message archival for messages older than {CutoffDate}", cutoffDate);

            // In a real implementation, this would move messages to an archive table or external storage
            // For now, we'll mark them as archived by updating a status or moving to a separate table
            
            var messagesToArchive = await _context.Messages
                .Where(m => m.ProcessedAt < cutoffDate && m.Status != MessageStatus.Archived)
                .ToListAsync();

            var count = messagesToArchive.Count;

            if (count > 0)
            {
                // Mark messages as archived
                foreach (var message in messagesToArchive)
                {
                    message.Status = MessageStatus.Archived;
                    message.UpdatedAt = DateTime.UtcNow;
                }

                await _context.SaveChangesAsync();

                _logger.LogInformation("Archived {Count} messages older than {Days} days", count, days);
            }
            else
            {
                _logger.LogInformation("No messages found for archival");
            }

            return count;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to archive old messages");
            throw;
        }
    }

    /// <summary>
    /// Deletes archived messages older than the archive retention period
    /// </summary>
    public async Task<int> DeleteArchivedMessagesAsync(int archiveRetentionDays = 0)
    {
        var days = archiveRetentionDays > 0 ? archiveRetentionDays : _defaultArchiveRetentionDays;
        var cutoffDate = DateTime.UtcNow.AddDays(-days);

        try
        {
            _logger.LogInformation("Starting deletion of archived messages older than {CutoffDate}", cutoffDate);

            var messagesToDelete = await _context.Messages
                .Where(m => m.Status == MessageStatus.Archived && m.UpdatedAt < cutoffDate)
                .ToListAsync();

            var count = messagesToDelete.Count;

            if (count > 0)
            {
                _context.Messages.RemoveRange(messagesToDelete);
                await _context.SaveChangesAsync();

                _logger.LogInformation("Deleted {Count} archived messages older than {Days} days", count, days);
            }
            else
            {
                _logger.LogInformation("No archived messages found for deletion");
            }

            return count;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete archived messages");
            throw;
        }
    }

    /// <summary>
    /// Gets a report on current data retention status
    /// </summary>
    public async Task<DataRetentionReport> GetRetentionReportAsync()
    {
        try
        {
            var report = new DataRetentionReport
            {
                GeneratedAt = DateTime.UtcNow,
                RetentionPolicyDays = _defaultRetentionDays,
                ArchiveRetentionPolicyDays = _defaultArchiveRetentionDays
            };

            var cutoffDate = DateTime.UtcNow.AddDays(-_defaultRetentionDays);
            var archiveCutoffDate = DateTime.UtcNow.AddDays(-_defaultArchiveRetentionDays);

            // Count messages by status
            report.TotalMessages = await _context.Messages.CountAsync();
            report.ActiveMessages = await _context.Messages
                .CountAsync(m => m.Status != MessageStatus.Archived);
            report.ArchivedMessages = await _context.Messages
                .CountAsync(m => m.Status == MessageStatus.Archived);

            // Count messages eligible for archival
            report.MessagesEligibleForArchival = await _context.Messages
                .CountAsync(m => m.ProcessedAt < cutoffDate && m.Status != MessageStatus.Archived);

            // Count archived messages eligible for deletion
            report.ArchivedMessagesEligibleForDeletion = await _context.Messages
                .CountAsync(m => m.Status == MessageStatus.Archived && m.UpdatedAt < archiveCutoffDate);

            // Get oldest and newest message dates
            if (report.TotalMessages > 0)
            {
                report.OldestMessageDate = await _context.Messages.MinAsync(m => m.ProcessedAt);
                report.NewestMessageDate = await _context.Messages.MaxAsync(m => m.ProcessedAt);
            }

            _logger.LogInformation("Data retention report generated: {TotalMessages} total, {EligibleForArchival} eligible for archival",
                report.TotalMessages, report.MessagesEligibleForArchival);

            return report;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to generate data retention report");
            throw;
        }
    }
}

public class DataRetentionReport
{
    public DateTime GeneratedAt { get; set; }
    public int RetentionPolicyDays { get; set; }
    public int ArchiveRetentionPolicyDays { get; set; }
    public int TotalMessages { get; set; }
    public int ActiveMessages { get; set; }
    public int ArchivedMessages { get; set; }
    public int MessagesEligibleForArchival { get; set; }
    public int ArchivedMessagesEligibleForDeletion { get; set; }
    public DateTime? OldestMessageDate { get; set; }
    public DateTime? NewestMessageDate { get; set; }
}
