    using LoggingService.Core;
    using LoggingService.Domain;
    using MassTransit;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Text.Json;
namespace LoggingService.Consumers;

    public class LogConsumer(ILogSevice _logService)  : IConsumer<JObject>
    {
   
    public async Task Consume(ConsumeContext<JObject> context)
    {
        JObject message = context.Message;

        var serviceName = message.Value<string>("Service");
        var content = message["Content"] as JObject;

        var log = new LogDto()
        {
            Service = serviceName,
            Content = new Content()
            {
                LogLevel = content?.Value<LogLevel>("LogLevel"),
                Message = content?.Value<string>("Message")
            }
        };

        _logService.WriteLog(log);
        await Task.CompletedTask;
    }
}


