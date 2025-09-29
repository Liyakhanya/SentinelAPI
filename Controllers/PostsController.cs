using Google.Cloud.Firestore;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SentinelApi.Models;
using SentinelApi.Services;
namespace SentinelApi.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class PostsController : BaseController
    {
        private readonly IFirebaseService _firebaseService;
        private readonly IFCMService _fcmService;
        private readonly IValidationService _validationService;
        private readonly ILogger<PostsController> _logger;
        public PostsController(IFirebaseService firebaseService, IFCMService fcmService, IValidationService validationService, ILogger<PostsController> logger)
        {
            _firebaseService = firebaseService;
            _fcmService = fcmService;
            _validationService = validationService;
            _logger = logger;
        }
        [Authorize]
        [HttpPost]
        public async Task<IActionResult> CreatePost([FromBody] PostRequest request)
        {
            try
            {
                var userId = GetUserId();
                if (string.IsNullOrEmpty(userId))
                {
                    return Error("User not authenticated", 401);
                }
                _logger.LogInformation("Post creation attempt by user: {UserId}", userId);
                // Validate inputs
                if (string.IsNullOrEmpty(request.Title) || request.Title.Length < 5)
                {
                    return Error("Title is required and must be at least 5 characters long");
                }
                if (!_validationService.IsValidSuburb(request.Suburb))
                {
                    return Error($"Invalid suburb: {request.Suburb}");
                }
                if (!string.IsNullOrEmpty(request.Category) && !_validationService.IsValidCategory(request.Category))
                {
                    return Error($"Invalid category: {request.Category}");
                }
                var user = await _firebaseService.GetUserAsync(userId);
                if (user == null)
                {
                    return Error("User not found", 404);
                }
                // Check group membership if groupId is provided
                if (!string.IsNullOrEmpty(request.GroupId))
                {
                    var isInGroup = await _firebaseService.UserExistsInGroupAsync(userId, request.GroupId);
                    if (!isInGroup)
                    {
                        return Error("User is not a member of the specified group", 403);
                    }
                    var group = await _firebaseService.GetGroupAsync(request.GroupId);
                    if (group == null || group.Suburb != request.Suburb)
                    {
                        return Error("Suburb does not match the group's suburb", 400);
                    }
                }
                // Create post
                var post = new Post
                {
                    PostId = Guid.NewGuid().ToString(),
                    UserId = request.Anonymous ? null : userId,
                    Title = request.Title.Trim(),
                    Description = request.Description?.Trim() ?? string.Empty,
                    Category = request.Category ?? "General",
                    Suburb = request.Suburb,
                    GroupId = request.GroupId,
                    MediaUrl = request.MediaUrl,
                    Location = request.Latitude.HasValue && request.Longitude.HasValue
                        ? new GeoPoint(request.Latitude.Value, request.Longitude.Value)
                        : null,
                    CreatedAt = DateTime.UtcNow
                };
                var postId = await _firebaseService.CreatePostAsync(post);
                await SendPostNotificationsAsync(post, user);
                _logger.LogInformation("Post created successfully: {PostId} by user {UserId}", postId, userId);
                return Success(new
                {
                    postId,
                    anonymous = request.Anonymous,
                    category = post.Category,
                    suburb = post.Suburb
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating post");
                return Error("Internal server error", 500);
            }
        }
        [Authorize]
        [HttpGet]
        public async Task<IActionResult> GetPosts([FromQuery] string? groupId, [FromQuery] string? category, [FromQuery] int limit = 50)
        {
            try
            {
                var userId = GetUserId();
                if (string.IsNullOrEmpty(userId))
                {
                    return Error("User not authenticated", 401);
                }
                var user = await _firebaseService.GetUserAsync(userId);
                if (user == null)
                {
                    return Error("User not found", 404);
                }
                List<Post> posts;
                if (!string.IsNullOrEmpty(groupId))
                {
                    // Check group membership
                    var isInGroup = await _firebaseService.UserExistsInGroupAsync(userId, groupId);
                    if (!isInGroup)
                    {
                        return Error("User is not a member of the specified group", 403);
                    }
                    posts = await _firebaseService.GetPostsAsync(user.Suburb, groupId, category, limit); // FIXED: Pass user.Suburb, but service handles optional
                }
                else if (!string.IsNullOrEmpty(category))
                {
                    if (!_validationService.IsValidCategory(category))
                    {
                        return Error($"Invalid category: {category}");
                    }
                    posts = await _firebaseService.GetPostsAsync(user.Suburb, null, category, limit);
                }
                else
                {
                    posts = await _firebaseService.GetPostsAsync(user.Suburb, null, null, limit);
                }
                // Filter posts based on user's notification categories if not in group
                if (string.IsNullOrEmpty(groupId))
                {
                    var userCategories = user.NotificationCategories ?? new List<string>();
                    posts = posts.Where(p => userCategories.Contains(p.Category)).ToList();
                }
                _logger.LogInformation("Retrieved {Count} posts for user: {UserId}", posts.Count, userId);
                return Success(new { posts, count = posts.Count });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving posts");
                return Error("Internal server error", 500);
            }
        }
        [Authorize]
        [HttpGet("location")]
        public async Task<IActionResult> GetPostsByLocation([FromQuery] double latitude, [FromQuery] double longitude, [FromQuery] double radius = 5.0, [FromQuery] string? category = null)
        {
            try
            {
                var userId = GetUserId();
                if (string.IsNullOrEmpty(userId))
                {
                    return Error("User not authenticated", 401);
                }
                if (radius < 0.1 || radius > 20.0)
                {
                    return Error("Radius must be between 0.1 and 20.0 km");
                }
                if (!string.IsNullOrEmpty(category) && !_validationService.IsValidCategory(category))
                {
                    return Error($"Invalid category: {category}");
                }
                // FIXED: Pass null for suburb to avoid filter
                var posts = await _firebaseService.GetPostsByLocationAsync(latitude, longitude, radius, category);
                _logger.LogInformation("Retrieved {Count} posts near location ({Lat}, {Lon}) for user: {UserId}",
                    posts.Count, latitude, longitude, userId);
                return Success(new { posts, count = posts.Count, radius });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving posts by location");
                return Error("Internal server error", 500);
            }
        }
        [Authorize]
        [HttpGet("hotspots")]
        public async Task<IActionResult> GetCrimeHotspots([FromQuery] string? suburb = null, [FromQuery] string? category = null)
        {
            try
            {
                var userId = GetUserId();
                if (string.IsNullOrEmpty(userId))
                {
                    return Error("User not authenticated", 401);
                }
                var user = await _firebaseService.GetUserAsync(userId);
                if (user == null)
                {
                    return Error("User not found", 404);
                }
                var targetSuburb = suburb ?? user.Suburb;
                if (!_validationService.IsValidSuburb(targetSuburb))
                {
                    return Error($"Invalid suburb: {targetSuburb}");
                }
                if (!string.IsNullOrEmpty(category) && !_validationService.IsValidCategory(category))
                {
                    return Error($"Invalid category: {category}");
                }
                var posts = await _firebaseService.GetPostsAsync(targetSuburb, null, category, 100);
                // Group by category for hotspot analysis
                var hotspots = posts
                    .GroupBy(p => p.Category)
                    .Select(g => new
                    {
                        Category = g.Key,
                        Count = g.Count(),
                        RecentPosts = g.OrderByDescending(p => p.CreatedAt).Take(5).ToList()
                    })
                    .OrderByDescending(h => h.Count)
                    .ToList();
                _logger.LogInformation("Retrieved hotspot data for suburb {Suburb}: {Count} categories", targetSuburb, hotspots.Count);
                return Success(new { suburb = targetSuburb, hotspots });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving crime hotspots");
                return Error("Internal server error", 500);
            }
        }
        private async Task SendPostNotificationsAsync(Post post, User postingUser)
        {
            try
            {
                List<User> targetUsers;
                if (!string.IsNullOrEmpty(post.GroupId))
                {
                    targetUsers = await _firebaseService.GetUsersByGroupAsync(post.GroupId);
                }
                else
                {
                    // Send to users in the same suburb who have this category enabled
                    targetUsers = await _firebaseService.GetUsersByNotificationCategoryAsync(post.Suburb, post.Category);
                }
                var fcmTokens = targetUsers
                    .Where(u => !string.IsNullOrEmpty(u.FCMToken))
                    .Select(u => u.FCMToken!)
                    .ToList();
                if (fcmTokens.Any())
                {
                    var title = !string.IsNullOrEmpty(post.GroupId)
                        ? $"New Alert in Group"
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
                    {
                        data.Add("groupId", post.GroupId);
                    }
                    await _fcmService.SendNotificationToUsersAsync(fcmTokens, title, post.Title, data);
                }
                _logger.LogInformation("Post notifications sent for post {PostId} to {Count} users", post.PostId, targetUsers.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending post notifications for post {PostId}", post.PostId);
            }
        }
    }
}