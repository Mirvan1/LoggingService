using Hangfire;

namespace LoggingService.Core.Jobs;

public class CleanOldLogJobs(IRecurringJobManager _recurringJobManager)
{
    public void CleanLogs()
    {
        _recurringJobManager.RemoveIfExists(nameof(CleanOldLogJobs));
        _recurringJobManager.AddOrUpdate<ILogSevice>("CleanOldLogs", service => service.CleanOldLogs(), Cron.Daily());

    }
}
