using LoggingService.Core;
using LoggingService.Domain;
using MassTransit;
using MassTransit.Transports;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Serilog;

namespace LoggingService.Controllers;
[Route("api/[controller]")]
[ApiController]
public class LogController : ControllerBase
{
    public ILogSevice _logService;
    IPublishEndpoint publishEndpoint;
    public LogController(ILogSevice logService, IPublishEndpoint publishEndpoint)

    {
        _logService = logService;
        this.publishEndpoint = publishEndpoint;
    }

    [HttpPost]
    public async Task<IActionResult> WriteLog([FromBody]LogDto request)
    {
        if (request == null) { return BadRequest(); }
        _logService.WriteLog(request);
        await publishEndpoint.Publish(request);
        return Ok();
    }
}
