using System;
using System.Diagnostics;
using System.Threading.Tasks;
using MassTransit;
using Microsoft.VisualBasic;
using Newtonsoft.Json.Linq;
using static System.Runtime.InteropServices.JavaScript.JSType;


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

    //Trace = 0
    //Debug = 1
    //Information = 2
    //Warning = 3
    //Error = 4
    //Critical = 5
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
