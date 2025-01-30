using LoggingService.Consumers;
using LoggingService.Core;
using MassTransit;
using Serilog;
using Serilog.Sinks.MSSqlServer;
using System.Collections.ObjectModel;
using Newtonsoft.Json.Linq;
using Hangfire;
using LoggingService.Core.Jobs;
using System.Runtime.InteropServices;
using LoggingService.Domain;

var builder = WebApplication.CreateBuilder(args);
builder.Logging.ClearProviders();
builder.Logging.AddConsole();
//builder.Logging.SetMinimumLevel(LogLevel.Debug);
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddScoped<ILogSevice,LogService> ();


var columnOptions = new ColumnOptions
{
    AdditionalColumns = new Collection<SqlColumn>
    {
        new SqlColumn { ColumnName = "ServiceName", DataType = System.Data.SqlDbType.NVarChar, DataLength = 128 },
        new SqlColumn { ColumnName = "LogLevel", DataType = System.Data.SqlDbType.NVarChar, DataLength = 50 },
        new SqlColumn { ColumnName = "LogMessage", DataType = System.Data.SqlDbType.NVarChar, DataLength = -1 }, 
        new SqlColumn { ColumnName = "LogId", DataType = System.Data.SqlDbType.NVarChar, DataLength = -1 }

    }
};
var rabbitMQConfig = builder.Configuration
    .GetSection("RabbitMQConfig")
    .Get<RabbitMQConfig>();


Log.Logger = new LoggerConfiguration()
      .MinimumLevel.Debug()
    .WriteTo.Console()
        .WriteTo.Seq(builder.Configuration.GetValue<string>("SeqUrl"))

        .Enrich.FromLogContext()
    .Enrich.WithProperty("Environment", builder.Environment.EnvironmentName)
 .Filter.ByExcluding(logEvent =>
        logEvent.Properties.TryGetValue("SourceContext", out var sourceContext) &&
        (sourceContext.ToString().Contains("Microsoft") || sourceContext.ToString().Contains("System") || sourceContext.ToString().Contains("MassTransit")
        || sourceContext.ToString().Contains("Hangfire")
        || sourceContext.ToString().Contains("Hangfire.Server")
)
    ).WriteTo.Console()
    .WriteTo.MSSqlServer(
        connectionString: builder.Configuration.GetValue<string>("LogConnectionString"),
        sinkOptions: new MSSqlServerSinkOptions
        {
            TableName = "Logs",
            AutoCreateSqlTable = true
        },
        columnOptions: columnOptions,
        restrictedToMinimumLevel: Serilog.Events.LogEventLevel.Information
    )
    .CreateLogger();

builder.Host.UseSerilog();

builder.Services.AddMassTransit(x =>
{
     x.AddConsumer<LogConsumer>();

    x.UsingRabbitMq((context, cfg) =>
    {
         cfg.UseNewtonsoftJsonSerializer();
        cfg.UseNewtonsoftJsonDeserializer();

         cfg.Host(rabbitMQConfig.Host, h =>
        {
            h.Username(rabbitMQConfig.Username);
            h.Password(rabbitMQConfig.Password);
        });
 
        cfg.ReceiveEndpoint("log_message_queue", e =>
        {
            e.ConfigureConsumeTopology = false;
             e.Bind("log-message");
            e.ConfigureConsumer<LogConsumer>(context);
        });
    });
});
builder.Services.AddMassTransitHostedService();


builder.Services.AddHangfire(configuration => configuration
 .SetDataCompatibilityLevel(CompatibilityLevel.Version_170)
            .UseSimpleAssemblyNameTypeSerializer()
            .UseRecommendedSerializerSettings()
        .UseSqlServerStorage(builder.Configuration.GetValue<string>("HangFireConnectionString")));
builder.Services.AddHangfireServer();


builder.Services.AddTransient<CleanOldLogJobs>();

var app = builder.Build();


using (var scope = app.Services.CreateScope())
{
    var jobScheduler = scope.ServiceProvider.GetRequiredService<CleanOldLogJobs>();
    jobScheduler.CleanLogs();
}

//if (app.Environment.IsDevelopment())
//{
    app.UseSwagger();
    app.UseSwaggerUI();
//  }
app.UseHangfireDashboard();

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();
