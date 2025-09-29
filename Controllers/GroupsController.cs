using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SentinelApi.Models;
using SentinelApi.Services;
namespace SentinelApi.Controllers
{
    [ApiController]
    [Route("api/groups")]
    public class GroupsController : BaseController
    {
        private readonly IFirebaseService _firebaseService;
        private readonly ILogger<GroupsController> _logger;
        private readonly IValidationService _validationService;
        public GroupsController(IFirebaseService firebaseService, IValidationService validationService, ILogger<GroupsController> logger)
        {
            _firebaseService = firebaseService;
            _validationService = validationService;
            _logger = logger;
        }
        [Authorize]
        [HttpPost("create")]
        public async Task<IActionResult> CreateGroup([FromBody] CreateGroupRequest request)
        {
            try
            {
                var userId = GetUserId();
                if (string.IsNullOrEmpty(userId))
                {
                    return Error("User not authenticated", 401);
                }
                _logger.LogInformation("Group creation attempt by user: {UserId}", userId);
                // Validate inputs
                if (!_validationService.IsValidSuburb(request.Suburb))
                {
                    var validSuburbs = string.Join(", ", _validationService.GetPESuburbs());
                    return Error($"Invalid suburb. Must be one of: {validSuburbs}");
                }
                // Create group
                var group = new Group
                {
                    Name = request.Name,
                    Suburb = request.Suburb,
                    CreatedBy = userId,
                    CreatedAt = DateTime.UtcNow
                };
                var groupId = await _firebaseService.CreateGroupAsync(group);
                // Add group to user's groups
                var user = await _firebaseService.GetUserAsync(userId);
                if (user == null)
                {
                    // FIXED: Added check for null user
                    _logger.LogWarning("User {UserId} not found after group creation", userId);
                    return Error("User not found after authentication", 404);
                }
                var currentGroups = user.Groups ?? new List<string>();
                var updates = new Dictionary<string, object>
                {
                    { "Groups", currentGroups.Concat(new[] { groupId }).ToList() }
                };
                await _firebaseService.UpdateUserAsync(userId, updates);
                _logger.LogInformation("Group created successfully: {GroupId} by user {UserId}", groupId, userId);
                return Success(new { groupId });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating group");
                return Error("Internal server error", 500);
            }
        }
        [Authorize]
        [HttpPost("join")]
        public async Task<IActionResult> JoinGroup([FromBody] JoinGroupRequest request)
        {
            try
            {
                var userId = GetUserId();
                if (string.IsNullOrEmpty(userId))
                {
                    return Error("User not authenticated", 401);
                }
                _logger.LogInformation("Group join attempt by user: {UserId} for group: {GroupId}", userId, request.GroupId);
                // Validate group exists
                var group = await _firebaseService.GetGroupAsync(request.GroupId);
                if (group == null)
                {
                    return Error("Group not found", 404);
                }
                // Add group to user's groups
                var user = await _firebaseService.GetUserAsync(userId);
                if (user == null)
                {
                    return Error("User not found", 404);
                }
                var currentGroups = user.Groups ?? new List<string>();
                if (currentGroups.Contains(request.GroupId))
                {
                    return Error("User is already a member of this group", 400);
                }
                var updates = new Dictionary<string, object>
                {
                    { "Groups", currentGroups.Concat(new[] { request.GroupId }).ToList() }
                };
                await _firebaseService.UpdateUserAsync(userId, updates);
                _logger.LogInformation("User {UserId} joined group {GroupId} successfully", userId, request.GroupId);
                return Success(new { success = true });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error joining group");
                return Error("Internal server error", 500);
            }
        }
        [Authorize]
        [HttpGet("{groupId}/posts")]
        public async Task<IActionResult> GetGroupPosts(string groupId)
        {
            try
            {
                var userId = GetUserId();
                if (string.IsNullOrEmpty(userId))
                {
                    return Error("User not authenticated", 401);
                }
                // Check group membership
                var isInGroup = await _firebaseService.UserExistsInGroupAsync(userId, groupId);
                if (!isInGroup)
                {
                    return Error("User is not a member of the specified group", 403);
                }
                var posts = await _firebaseService.GetGroupPostsAsync(groupId);
                _logger.LogInformation("Retrieved {Count} posts for group {GroupId}", posts.Count, groupId);
                return Success(posts);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving group posts");
                return Error("Internal server error", 500);
            }
        }
        [Authorize]
        [HttpGet]
        public async Task<IActionResult> GetGroups()
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
                var groups = await _firebaseService.GetGroupsBySuburbAsync(user.Suburb);
                _logger.LogInformation("Retrieved {Count} groups for user {UserId}", groups.Count, userId);
                return Success(groups);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving groups");
                return Error("Internal server error", 500);
            }
        }
    }
}