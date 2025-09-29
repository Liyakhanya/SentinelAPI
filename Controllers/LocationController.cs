using Google.Cloud.Firestore;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SentinelApi.Models;
using SentinelApi.Services;

namespace SentinelApi.Controllers
{
    [ApiController]
    [Route("api/location")]
    public class LocationController : BaseController
    {
        private readonly IFirebaseService _firebaseService;
        private readonly IFCMService _fcmService;
        private readonly ILogger<LocationController> _logger;

        public LocationController(IFirebaseService firebaseService, IFCMService fcmService, ILogger<LocationController> logger)
        {
            _firebaseService = firebaseService;
            _fcmService = fcmService;
            _logger = logger;
        }

        [Authorize]
        [HttpPost("share")]
        public async Task<IActionResult> ShareLocation([FromBody] LocationShareRequest request)
        {
            try
            {
                var userId = GetUserId();
                if (string.IsNullOrEmpty(userId))
                {
                    return Error("User not authenticated", 401);
                }

                _logger.LogInformation("Location share attempt by user: {UserId}", userId);

                var user = await _firebaseService.GetUserAsync(userId);
                if (user == null)
                {
                    return Error("User not found", 404);
                }

                // Validate contact emails
                foreach (var contact in request.Contacts)
                {
                    if (!IsValidEmail(contact))
                    {
                        return Error($"Invalid contact email: {contact}");
                    }
                }

                // Create location share
                var share = new LocationShare
                {
                    UserId = userId,
                    Contacts = request.Contacts.Distinct().ToList(),
                    Duration = request.Duration,
                    Location = new GeoPoint(request.Latitude, request.Longitude),
                    CreatedAt = DateTime.UtcNow,
                    ExpiresAt = DateTime.UtcNow.AddMinutes(request.Duration)
                };

                var shareId = await _firebaseService.CreateLocationShareAsync(share);

                // Send FCM notifications to contacts
                await SendLocationShareNotificationsAsync(user, share);

                _logger.LogInformation("Location shared successfully: {ShareId} by user {UserId}", shareId, userId);
                return Success(new { shareId });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sharing location");
                return Error("Internal server error", 500);
            }
        }

        private async Task SendLocationShareNotificationsAsync(User user, LocationShare share)
        {
            try
            {
                var contactUsers = await _firebaseService.GetUsersByEmailsAsync(share.Contacts);
                var fcmTokens = contactUsers
                    .Where(u => !string.IsNullOrEmpty(u.FCMToken))
                    .Select(u => u.FCMToken!)
                    .ToList();
                if (fcmTokens.Any())
                {
                    var latitude = share.Location.HasValue ? share.Location.Value.Latitude.ToString() : "";
                    var longitude = share.Location.HasValue ? share.Location.Value.Longitude.ToString() : "";
                    var body = string.IsNullOrEmpty(latitude) ? $"Location shared for {share.Duration} minutes"
                        : $"Location shared for {share.Duration} minutes at [{latitude}, {longitude}]";
                    var data = new Dictionary<string, string>
            {
                { "shareId", share.ShareId },
                { "userId", user.UserId },
                { "latitude", latitude },
                { "longitude", longitude },
                { "duration", share.Duration.ToString() },
                { "type", "location_share" }
            };
                    await _fcmService.SendNotificationToUsersAsync(
                        fcmTokens,
                        $"Location Shared by {user.Email}",
                        body,
                        data);
                    _logger.LogInformation("Location share FCM notifications sent to {Count} contacts for user {UserId}", fcmTokens.Count, user.UserId);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending location share FCM notifications for user {UserId}", user.UserId);
            }
        }

        private bool IsValidEmail(string email)
        {
            if (string.IsNullOrWhiteSpace(email)) return false;
            try
            {
                var addr = new System.Net.Mail.MailAddress(email);
                return addr.Address == email && email.Contains('@') && email.Contains('.');
            }
            catch
            {
                return false;
            }
        }
    }
}