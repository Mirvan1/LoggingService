using System;
using System.Threading.Tasks;
using MassTransit;
using Newtonsoft.Json.Linq;


var busControl = Bus.Factory.CreateUsingRabbitMq(cfg =>
{
    cfg.UseNewtonsoftJsonSerializer();
    cfg.UseNewtonsoftJsonSerializer();

    cfg.Host("localhost", "/", h =>
    {
        h.Username("guest");
        h.Password("guest");
    });

    cfg.Message<JObject>(x =>
    {
        x.SetEntityName("log-message");
    });
});

await busControl.StartAsync();
try
{
    JObject jsonMessage = new JObject
            {
                { "Service", "TestLogService" },
                { "Content", new JObject
                    {
                        { "LogLevel", "1" },
                        { "Message", "Test log ." }
                    }
                }
            };

    var sendEndpoint = await busControl.GetSendEndpoint(new Uri("queue:log_message_queue"));
    await sendEndpoint.Send(jsonMessage);


    Console.ReadKey();
}
catch (Exception ex)
{
    Console.WriteLine("Error: " + ex.Message);
}
finally
{
    await busControl.StopAsync();
}
