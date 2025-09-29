using FirebaseAdmin.Messaging;
using Google.Cloud.Firestore;
using SentinelApi.Models;

namespace SentinelApi.Services
{
    public interface IFCMService
    {
        Task<bool> SendNotificationToUsersAsync(List<string> fcmTokens, string title, string body, Dictionary<string, string>? data = null);
        Task<bool> SendPostNotificationAsync(Post post, List<User> targetUsers);
        Task<bool> SendPanicNotificationAsync(PanicAlert alert, User user, List<User> contacts);
        Task<bool> SendLocationShareNotificationAsync(LocationShare share, User user, List<User> contacts);
    }

    public class FCMService : IFCMService
    {
        private readonly ILogger<FCMService> _logger;
        private readonly IFirebaseService _firebaseService;

        public FCMService(ILogger<FCMService> logger, IFirebaseService firebaseService)
        {
            _logger = logger;
            _firebaseService = firebaseService;
        }

        public async Task<bool> SendNotificationToUsersAsync(List<string> fcmTokens, string title, string body, Dictionary<string, string>? data = null)
        {
            if (fcmTokens == null || !fcmTokens.Any())
            {
                _logger.LogWarning("No FCM tokens provided for notification");
                return false;
            }

            try
            {
                var chunks = fcmTokens.Chunk(500);
                var successCount = 0;
                var invalidTokens = new List<string>();

                foreach (var chunk in chunks)
                {
                    var message = new MulticastMessage()
                    {
                        Tokens = chunk.ToList(),
                        Notification = new Notification
                        {
                            Title = title,
                            Body = body
                        },
                        Data = data ?? new Dictionary<string, string>(),
                        Android = new AndroidConfig
                        {
                            Priority = Priority.High,
                            Notification = new AndroidNotification
                            {
                                ChannelId = "safeconnect_alerts",
                                DefaultSound = true,
                                DefaultVibrateTimings = true
                            }
                        },
                        Apns = new ApnsConfig
                        {
                            Headers = new Dictionary<string, string>
                            {
                                { "apns-priority", "10" }
                            },
                            Aps = new Aps
                            {
                                ContentAvailable = true,
                                Sound = "default",
                                Badge = 1
                            }
                        }
                    };

                    var response = await FirebaseMessaging.DefaultInstance.SendEachForMulticastAsync(message);
                    successCount += response.SuccessCount;

                    if (response.FailureCount > 0)
                    {
                        for (int i = 0; i < response.Responses.Count; i++)
                        {
                            var resp = response.Responses[i];
                            if (!resp.IsSuccess && resp.Exception != null)
                            {
                                _logger.LogError("FCM send error for token {Token}: {Error}", chunk[i], resp.Exception.Message);
                                if (resp.Exception.MessagingErrorCode == MessagingErrorCode.Unregistered || resp.Exception.MessagingErrorCode == MessagingErrorCode.InvalidArgument)
                                {
                                    invalidTokens.Add(chunk[i]);
                                }
                            }
                        }
                    }
                }

                // Cleanup invalid tokens from user docs
                if (invalidTokens.Any())
                {
                    await CleanupInvalidTokensAsync(invalidTokens);
                }

                _logger.LogInformation("FCM notifications sent. Total success: {SuccessCount}", successCount);
                return successCount > 0;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending FCM notification");
                return false;
            }
        }

        private async Task CleanupInvalidTokensAsync(List<string> invalidTokens)
        {
            try
            {
                foreach (var token in invalidTokens)
                {
                    // Find users with this token and remove it
                    var query = _firebaseService.GetFirestoreDb().Collection("users").WhereEqualTo("FCMToken", token);
                    var snapshot = await query.GetSnapshotAsync();
                    foreach (var doc in snapshot.Documents)
                    {
                        await doc.Reference.UpdateAsync("FCMToken", null);
                        _logger.LogInformation("Removed invalid FCM token {Token} from user {UserId}", token, doc.Id);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error cleaning up invalid FCM tokens");
            }
        }

        public async Task<bool> SendPostNotificationAsync(Post post, List<User> targetUsers)
        {
            var fcmTokens = targetUsers
                .Where(u => !string.IsNullOrEmpty(u.FCMToken))
                .Select(u => u.FCMToken!)
                .ToList();

            if (!fcmTokens.Any()) return false;

            var title = !string.IsNullOrEmpty(post.GroupId)
                ? $"New Alert in {post.GroupId}"
                : $"New Alert in {post.Suburb}";

            var data = new Dictionary<string, string>
            {
                { "type", "post" },
                { "postId", post.PostId },
                { "category", post.Category },
                { "suburb", post.Suburb },
                { "timestamp", DateTime.UtcNow.ToString("O") }
            };

            if (!string.IsNullOrEmpty(post.GroupId))
                data.Add("groupId", post.GroupId);

            return await SendNotificationToUsersAsync(fcmTokens, title, post.Title, data);
        }

        public async Task<bool> SendPanicNotificationAsync(PanicAlert alert, User user, List<User> contacts)
        {
            var fcmTokens = contacts
                .Where(u => !string.IsNullOrEmpty(u.FCMToken))
                .Select(u => u.FCMToken!)
                .ToList();

            if (!fcmTokens.Any()) return false;

            string locationText = "";

            // FIXED: Check HasValue before accessing properties
            if (alert.Location.HasValue)
            {
                // FIXED: Use .Value to access the actual GeoPoint
                locationText = $" at [{alert.Location.Value.Latitude:F4}, {alert.Location.Value.Longitude:F4}]";
            }

            var data = new Dictionary<string, string>
            {
                { "type", "panic" },
                { "panicId", alert.PanicId },
                { "userId", user.UserId },
                { "userEmail", user.Email },
                { "timestamp", DateTime.UtcNow.ToString("O") }
            };

            // FIXED: Check HasValue before accessing properties
            if (alert.Location.HasValue)
            {
                // FIXED: Use .Value to access the actual GeoPoint
                data.Add("latitude", alert.Location.Value.Latitude.ToString());
                data.Add("longitude", alert.Location.Value.Longitude.ToString());
            }

            return await SendNotificationToUsersAsync(
                fcmTokens,
                $"SOS Alert from {user.Email}",
                $"{alert.Message}{locationText}",
                data);
        }

        public async Task<bool> SendLocationShareNotificationAsync(LocationShare share, User user, List<User> contacts)
        {
            var fcmTokens = contacts
                .Where(u => !string.IsNullOrEmpty(u.FCMToken))
                .Select(u => u.FCMToken!)
                .ToList();

            if (!fcmTokens.Any()) return false;

            var data = new Dictionary<string, string>
            {
                { "type", "location_share" },
                { "shareId", share.ShareId },
                { "userId", user.UserId },
                { "userEmail", user.Email },
                { "duration", share.Duration.ToString() },
                { "timestamp", DateTime.UtcNow.ToString("O") }
            };

            // FIXED: Check HasValue before accessing properties
            if (share.Location.HasValue)
            {
                // FIXED: Use .Value to access the actual GeoPoint
                data.Add("latitude", share.Location.Value.Latitude.ToString());
                data.Add("longitude", share.Location.Value.Longitude.ToString());
            }

            return await SendNotificationToUsersAsync(
                fcmTokens,
                $"Location Shared by {user.Email}",
                $"Location shared for {share.Duration} minutes",
                data);
        }
    }
}