using Microsoft.AspNetCore.Mvc;
using ProgKidsNotifier.Services;

namespace ProgKids.Notification.API.Controllers;

[ApiController]
[Route("[controller]")]
public class BotPanel : ControllerBase
{
    private IServiceProvider _serviceProvider;

    public BotPanel(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }
    [HttpPost("[action]")]
    public async Task<ActionResult<string>> SendUpdateOfPost([FromBody]dataDto dto)
    {
        try
        {
            if (string.IsNullOrEmpty(dto.secret) || string.IsNullOrEmpty(dto.columnName) || string.IsNullOrEmpty(dto.newValue))
                return BadRequest($"emtpy data");
            if (dto.secret != "p]QV3G$mn6T0") return BadRequest($"Unauthorized");

            var firstPart = dto.columnName switch
            {
                "Status" => "**Статус:** ",
                "Agent" => "** Взял в работу: ** ",
                "contactDate" => "** Дата связи с учеником: ** ",
                "contactTime" => "** Время связи с учеником **",
                "Retries" => "** Попытка связи с клиентом:  **",
                "Result" => "** Результат выполнения: **",
                _ => "ошибка"
            }; 
            
            var result = await MonitorService.SendUpdateMessage(dto.postId, firstPart + dto.newValue);
            return Ok(result.ToString());
        }   
        catch (Exception ex)
        {
            return StatusCode(StatusCodes.Status500InternalServerError, ex.Message);
        }
    }

    [HttpGet("/status")]

    public async Task<ActionResult<string>> StatusOfBot()
        => (MonitorService.ServiceOn)? "Сервис на работе" : "Сервис не на работе, напишите разрабочику";
    
    public record dataDto(string? secret, string? columnName, string? newValue, string? postId);
}