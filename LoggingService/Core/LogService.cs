using Azure.Core;
using LoggingService.Domain;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;

namespace LoggingService.Core;

public class LogService(ILogger<LogService> _logger,IConfiguration _configuration) :ILogSevice
{
 

    public void WriteLog(LogDto dto)
    {
 
        using (Serilog.Context.LogContext.PushProperty("LogId", Guid.NewGuid().ToString()))
        using (Serilog.Context.LogContext.PushProperty("LogLevel", dto.Content.LogLevel.ToString()))
        using (Serilog.Context.LogContext.PushProperty("LogMessage", dto.Content.Message))
        using (Serilog.Context.LogContext.PushProperty("ServiceName", dto.Service))
        {
            _logger.Log(dto.Content.LogLevel??LogLevel.Error,dto.Content.Message);

        }
    }


    public async Task CleanOldLogs()
    {
          
            string commandText = @"
            DELETE FROM Logs 
            WHERE Timestamp < DATEADD(day, -1, GETDATE())
             ";

        using (var connection = new SqlConnection(_configuration.GetValue<string>("LogConnectionString")))
        {
            await connection.OpenAsync();

            using (var command = new SqlCommand(commandText, connection))
            {
               await command.ExecuteNonQueryAsync();
            }
            await connection.CloseAsync();
        }
    }

}
