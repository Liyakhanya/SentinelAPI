using Google.Cloud.Firestore;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SentinelApi.Models;
using SentinelApi.Services;

namespace SentinelApi.Controllers
{
    [ApiController]
    [Route("api/panic")]
    public class PanicController : BaseController
    {
        private readonly IFirebaseService _firebaseService;
        private readonly IFCMService _fcmService;
        private readonly ILogger<PanicController> _logger;

        public PanicController(IFirebaseService firebaseService, IFCMService fcmService, ILogger<PanicController> logger)
        {
            _firebaseService = firebaseService;
            _fcmService = fcmService;
            _logger = logger;
        }

        [Authorize]
        [HttpPost]
        public async Task<IActionResult> SendPanicAlert([FromBody] PanicRequest request)
        {
            try
            {
                var userId = GetUserId();
                if (string.IsNullOrEmpty(userId))
                    return Error("User not authenticated", 401);

                _logger.LogInformation("Panic alert attempt by user: {UserId}", userId);

                var user = await _firebaseService.GetUserAsync(userId);
                if (user == null)
                    return Error("User not found", 404);

                if (user.TrustedContacts == null || !user.TrustedContacts.Any())
                    return Error("No trusted contacts configured", 400);

                var panicId = Guid.NewGuid().ToString();
                var alert = new PanicAlert
                {
                    PanicId = panicId,
                    UserId = userId,
                    Message = string.IsNullOrWhiteSpace(request.Message) ? "Emergency!" : request.Message.Trim(),
                    Location = (request.Latitude.HasValue && request.Longitude.HasValue)
                        ? new GeoPoint(request.Latitude.Value, request.Longitude.Value)
                        : null,
                    CreatedAt = DateTime.UtcNow
                };

                await _firebaseService.CreatePanicAlertAsync(alert);
                await SendPanicNotificationsAsync(user, alert);

                _logger.LogInformation("Panic alert sent successfully: {PanicId} by user {UserId}", panicId, userId);
                return Success(new { success = true, panicId });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending panic alert");
                return Error("Internal server error", 500);
            }
        }

        private async Task SendPanicNotificationsAsync(User user, PanicAlert alert)
        {
            try
            {
                var trustedUsers = await _firebaseService.GetUsersByEmailsAsync(user.TrustedContacts ?? new List<string>());
                var fcmTokens = trustedUsers
                    .Where(u => !string.IsNullOrEmpty(u.FCMToken))
                    .Select(u => u.FCMToken!)
                    .ToList();
                if (!fcmTokens.Any())
                {
                    _logger.LogWarning("No FCM tokens found for trusted contacts of user {UserId}", user.UserId);
                    return;
                }
                string locationText = alert.Location.HasValue
                    ? $" at [{alert.Location.Value.Latitude:F4}, {alert.Location.Value.Longitude:F4}]"
                    : string.Empty;
                var data = new Dictionary<string, string>
        {
            { "panicId", alert.PanicId },
            { "userId", user.UserId },
            { "type", "panic" },
            { "timestamp", DateTime.UtcNow.ToString("O") }
        };
                if (alert.Location.HasValue)
                {
                    data.Add("latitude", alert.Location.Value.Latitude.ToString());
                    data.Add("longitude", alert.Location.Value.Longitude.ToString());
                }
                await _fcmService.SendNotificationToUsersAsync(
                    fcmTokens,
                    $"🚨 SOS Alert from {user.Email}",
                    $"{alert.Message}{locationText}",
                    data);
                _logger.LogInformation("Panic FCM notifications sent to {Count} contacts for user {UserId}", fcmTokens.Count, user.UserId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending panic FCM notifications for user {UserId}", user.UserId);
                throw;
            }
        }
    }
}