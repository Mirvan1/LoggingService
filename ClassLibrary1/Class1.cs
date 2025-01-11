namespace LoggingService.Domain;

public class LogDto
{
    public string Service { get; set; }
    public Content Content { get; set; }
}

public class Content
{
    public string LogLevel { get; set; }
    public string Message { get; set; }
}