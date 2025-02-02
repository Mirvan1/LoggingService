using LoggingService.Consumers;
using LoggingService.Core;
using MassTransit;
using Serilog;
using System.Collections.ObjectModel;
using Newtonsoft.Json.Linq;
using Hangfire;
using LoggingService.Core.Jobs;
using System.Runtime.InteropServices;
using LoggingService.Domain;
using Npgsql;
using Serilog.Sinks.PostgreSQL;
using Hangfire.PostgreSql;

var builder = WebApplication.CreateBuilder(args);
builder.Logging.ClearProviders();
builder.Logging.AddConsole();
//builder.Logging.SetMinimumLevel(LogLevel.Debug);
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
string createLoggingDB = "CREATE DATABASE LoggingDB";
string createHangFireDB = "CREATE DATABASE HangFireLoggingDB";



string adminConn = builder.Configuration.GetConnectionString("AdminConnectionString");

//try
//{

//    // var adminConnectionString = builder.Configuration.GetValue<string>("LogConnectionString");
//    using var connection = new NpgsqlConnection(adminConn);
//    connection.Open();

//    using (var command = new NpgsqlCommand(createLoggingDB, connection))
//    {
//        command.ExecuteNonQuery();
//    }

//}
//catch (Exception ex)
//{
//    Console.WriteLine("Error " + ex.Message);
//}



//try
//{
//    //   var adminConnectionString = builder.Configuration.GetValue<string>("HangFireConnectionString");
//    using var connection = new NpgsqlConnection(adminConn);
//    connection.Open();

//    using (var command = new NpgsqlCommand(createHangFireDB, connection))
//    {
//        command.ExecuteNonQuery();
//    }

//}
//catch (Exception ex)
//{
//    Console.WriteLine("Erro: " + ex.Message);
//}

 if (!DatabaseExists("LoggingDB", adminConn))
{
    CreateDatabase("LoggingDB", adminConn);
}

 if (!DatabaseExists("HangFireLoggingDB", adminConn))
{
    CreateDatabase("HangFireLoggingDB", adminConn);
}


builder.Services.AddScoped<ILogSevice, LogService>();





//var columnOptions = new ColumnOptions
//{
//    AdditionalColumns = new Collection<SqlColumn>
//    {
//        new SqlColumn { ColumnName = "ServiceName", DataType = System.Data.SqlDbType.NVarChar, DataLength = 128 },
//        new SqlColumn { ColumnName = "LogLevel", DataType = System.Data.SqlDbType.NVarChar, DataLength = 50 },
//        new SqlColumn { ColumnName = "LogMessage", DataType = System.Data.SqlDbType.NVarChar, DataLength = -1 }, 
//        new SqlColumn { ColumnName = "LogId", DataType = System.Data.SqlDbType.NVarChar, DataLength = -1 }

//    }
//};

var columnWriters = new Dictionary<string, ColumnWriterBase>
{
    { "timestamp", new TimestampColumnWriter() }, // default
    { "level", new LevelColumnWriter() },
    { "message", new RenderedMessageColumnWriter() },
    { "exception", new ExceptionColumnWriter() },

     { "ServiceName", new RenderedMessageColumnWriter() },
    { "LogLevel", new RenderedMessageColumnWriter() },
    { "LogMessage", new RenderedMessageColumnWriter() },
    { "LogId", new RenderedMessageColumnWriter() }
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
        (sourceContext.ToString().Contains("Microsoft") ||
         sourceContext.ToString().Contains("System") ||
         sourceContext.ToString().Contains("MassTransit") ||
         sourceContext.ToString().Contains("Hangfire") ||
         sourceContext.ToString().Contains("Hangfire.Server")
        )
    )
    .WriteTo.PostgreSQL(
        connectionString: builder.Configuration.GetValue<string>("LogConnectionString"),
        tableName: "Logs",
        needAutoCreateTable: true,
        columnOptions: columnWriters
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
        .UsePostgreSqlStorage(builder.Configuration.GetSection("ConnectionStrings:HangFireConnectionString").Value));
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

 
     bool DatabaseExists(string databaseName, string connectionString)
{
    using var connection = new NpgsqlConnection(connectionString);
    connection.Open();

    var commandText = @"
        SELECT 1 FROM pg_database 
        WHERE datname = @databaseName";

    using var command = new NpgsqlCommand(commandText, connection);
    command.Parameters.AddWithValue("@databaseName", databaseName);

    return command.ExecuteScalar() != null;
}

     void CreateDatabase(string databaseName, string connectionString)
{
    try
    {
         var builder = new NpgsqlConnectionStringBuilder(connectionString)
        {
            Database = "postgres"   
        };

        using var connection = new NpgsqlConnection(builder.ConnectionString);
        connection.Open();

        var createCommand = $"CREATE DATABASE \"{databaseName}\"";
        using var cmd = new NpgsqlCommand(createCommand, connection);
        cmd.ExecuteNonQuery();
        Console.WriteLine($"Created database {databaseName}");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Error creating database {databaseName}: {ex.Message}");
    }
}

