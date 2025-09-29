using Microsoft.AspNetCore.Mvc;
using SentinelApi.Models;
using System.Security.Claims;
namespace SentinelApi.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class BaseController : ControllerBase
    {
        protected IActionResult Success<T>(T data, string? message = null)
        {
            var response = new BaseResponse<T>
            {
                Success = true,
                Data = data,
                Message = message
            };
            return Ok(response);
        }
        protected IActionResult Error(string error, int statusCode = 400)
        {
            var response = new BaseResponse<object?>
            {
                Success = false,
                Error = error
            };
            return StatusCode(statusCode, response);
        }
        protected IActionResult Created<T>(T data, string message = "Resource created successfully")
        {
            var response = new BaseResponse<T>
            {
                Success = true,
                Data = data,
                Message = message
            };
            return StatusCode(201, response);
        }
        protected string? GetUserId()
        {
            return User?.Claims.FirstOrDefault(c => c.Type == "user_id")?.Value;
        }
        protected string? GetUserEmail()
        {
            return User?.Claims.FirstOrDefault(c => c.Type == "email")?.Value;
        }
    }
}