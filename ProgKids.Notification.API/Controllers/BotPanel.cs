using Microsoft.AspNetCore.Mvc;
using ProgKidsNotifier.Services;

namespace ProgKids.Notification.API.Controllers;

[ApiController]
[Route("[controller]")]
public class BotPanel : ControllerBase
{
    
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
                "Agent" => "*Взял в работу:* ",
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

    public record dataDto(string? secret, string? columnName, string? newValue, string? postId);
}