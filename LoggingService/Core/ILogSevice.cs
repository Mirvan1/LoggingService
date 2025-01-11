using LoggingService.Domain;

namespace LoggingService.Core;

public interface ILogSevice
{
    void WriteLog(LogDto dto);
    Task CleanOldLogs();
}