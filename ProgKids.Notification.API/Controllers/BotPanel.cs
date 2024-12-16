using Microsoft.AspNetCore.Mvc;
using ProgKidsNotifier.Services;

namespace ProgKids.Notification.API.Controllers;

[ApiController]
[Route("[controller]")]
public class BotPanel : ControllerBase
{
    
    [HttpPost("[action]")]
    public async Task<ActionResult<string>> SendUpdateOfPost(string? secret, string? columnName, string? newValue, string? postId)
    {
        try
        {
            if (string.IsNullOrEmpty(secret) || string.IsNullOrEmpty(columnName) || string.IsNullOrEmpty(newValue))
                return BadRequest($"emtpy data");
            if (secret != "p]QV3G$mn6T0") return BadRequest($"Unauthorized");

            var firstPart = columnName switch
            {
                "Status" => "**Статус:** ",
                "Agent" => "*Взял в работу:* ",
                _ => "ошибка"
            }; 
            
            var result = await MonitorService.SendUpdateMessage(postId, firstPart + newValue);
            return Ok(result.ToString());
        }   
        catch (Exception ex)
        {
            return StatusCode(StatusCodes.Status500InternalServerError, ex.Message);
        }
    }
}