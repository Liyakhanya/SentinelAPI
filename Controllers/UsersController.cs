using FirebaseAdmin.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SentinelApi.Models;
using SentinelApi.Services;

namespace SentinelApi.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class UsersController : BaseController
    {
        private readonly IFirebaseService _firebaseService;
        private readonly IValidationService _validationService;
        private readonly ILogger<UsersController> _logger;

        public UsersController(IFirebaseService firebaseService, IValidationService validationService, ILogger<UsersController> logger)
        {
            _firebaseService = firebaseService;
            _validationService = validationService;
            _logger = logger;
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginRequest request)
        {
            try
            {
                _logger.LogInformation("Login attempt for email: {Email}", request.Email);

                // Validate inputs
                if (string.IsNullOrEmpty(request.Email) || !IsValidEmail(request.Email))
                {
                    return Error("Valid email address is required");
                }

                if (string.IsNullOrEmpty(request.Password))
                {
                    return Error("Password is required");
                }

                // Verify user exists in Firebase Auth and get their data
                UserRecord userRecord;
                try
                {
                    userRecord = await FirebaseAuth.DefaultInstance.GetUserByEmailAsync(request.Email);
                }
                catch (FirebaseAuthException ex) when (ex.AuthErrorCode == FirebaseAdmin.Auth.AuthErrorCode.UserNotFound)
                {
                    return Error("Invalid email or password", 401);
                }
                catch (FirebaseAuthException ex)
                {
                    _logger.LogError(ex, "Firebase auth error during login");
                    return Error("Authentication failed: " + ex.Message, 401);
                }

                // Get user data from Firestore
                var user = await _firebaseService.GetUserAsync(userRecord.Uid);
                if (user == null)
                {
                    return Error("User profile not found", 404);
                }

                // Update FCM token if provided
                if (!string.IsNullOrEmpty(request.FCMToken))
                {
                    var updates = new Dictionary<string, object>
                    {
                        { "FCMToken", request.FCMToken },
                        { "UpdatedAt", DateTime.UtcNow }
                    };
                    await _firebaseService.UpdateUserAsync(user.UserId, updates);
                }

                _logger.LogInformation("User logged in successfully: {UserId}", user.UserId);

                return Success(new
                {
                    user = new
                    {
                        user.UserId,
                        user.Email,
                        user.Suburb,
                        user.NotificationCategories,
                        user.DarkMode,
                        user.AnonymousMode,
                        user.LocationSharingDuration,
                        user.CreatedAt
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during user login");
                return Error("Internal server error", 500);
            }
        }

        [HttpPost("register")]
        public async Task<IActionResult> Register([FromBody] RegisterRequest request)
        {
            try
            {
                _logger.LogInformation("Registration attempt for email: {Email}", request.Email);
                // Validate inputs
                if (string.IsNullOrEmpty(request.Email) || !IsValidEmail(request.Email))
                {
                    return Error("Valid email address is required");
                }
                if (!_validationService.IsValidSuburb(request.Suburb))
                {
                    var validSuburbs = string.Join(", ", _validationService.GetPESuburbs());
                    return Error($"Invalid suburb. Must be one of: {validSuburbs}");
                }
                if (request.TrustedContacts == null || !request.TrustedContacts.Any())
                {
                    return Error("At least one trusted contact is required");
                }
                // FIXED: Validate trusted contact emails and deduplicate
                request.TrustedContacts = request.TrustedContacts.Distinct().ToList();
                foreach (var contact in request.TrustedContacts)
                {
                    if (!IsValidEmail(contact))
                    {
                        return Error($"Invalid trusted contact email: {contact}");
                    }
                }
                // Check if user already exists
                try
                {
                    var existingUser = await FirebaseAuth.DefaultInstance.GetUserByEmailAsync(request.Email);
                    if (existingUser != null)
                    {
                        return Error("User with this email already exists", 409);
                    }
                }
                catch (FirebaseAuthException)
                {
                    // User doesn't exist, which is what we want
                }
                // Create user in Firebase Auth
                UserRecord userRecord;
                try
                {
                    userRecord = await FirebaseAuth.DefaultInstance.CreateUserAsync(new UserRecordArgs
                    {
                        Email = request.Email,
                        Password = request.Password, // FIXED: Added password
                        EmailVerified = false,
                        Disabled = false
                    });
                }
                catch (FirebaseAuthException ex)
                {
                    _logger.LogError(ex, "Firebase auth error during registration");
                    return Error("Failed to create user account: " + ex.Message, 500);
                }
                // Create user document with comprehensive defaults
                var user = new User
                {
                    UserId = userRecord.Uid,
                    Email = request.Email,
                    Suburb = request.Suburb,
                    TrustedContacts = request.TrustedContacts,
                    FCMToken = request.FCMToken,
                    NotificationCategories = new List<string> { "Robbery", "GBV", "Hazard", "General" },
                    DarkMode = false,
                    AnonymousMode = false,
                    Groups = new List<string>(),
                    LocationSharingDuration = 30,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };
                await _firebaseService.CreateUserAsync(user);
                _logger.LogInformation("User registered successfully: {UserId}", user.UserId);
                return Success(new { userId = user.UserId, email = user.Email, suburb = user.Suburb });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during user registration");
                return Error("Internal server error", 500);
            }
        }

        [Authorize]
        [HttpPost("settings")]
        public async Task<IActionResult> UpdateSettings([FromBody] SettingsRequest request)
        {
            try
            {
                var userId = GetUserId();
                if (string.IsNullOrEmpty(userId))
                {
                    return Error("User not authenticated", 401);
                }
                _logger.LogInformation("Settings update attempt for user: {UserId}", userId);
                var user = await _firebaseService.GetUserAsync(userId);
                if (user == null)
                {
                    return Error("User not found", 404);
                }
                var updates = new Dictionary<string, object>();
                // Validate and add updates
                if (!string.IsNullOrEmpty(request.Suburb))
                {
                    if (!_validationService.IsValidSuburb(request.Suburb))
                    {
                        var validSuburbs = string.Join(", ", _validationService.GetPESuburbs());
                        return Error($"Invalid suburb. Must be one of: {validSuburbs}");
                    }
                    updates["Suburb"] = request.Suburb;
                }
                if (request.NotificationCategories != null && request.NotificationCategories.Any())
                {
                    var invalidCategories = request.NotificationCategories
                        .Where(c => !_validationService.IsValidCategory(c))
                        .ToList();
                    if (invalidCategories.Any())
                    {
                        var validCategories = string.Join(", ", _validationService.GetValidCategories());
                        return Error($"Invalid categories: {string.Join(", ", invalidCategories)}. Valid categories: {validCategories}");
                    }
                    updates["NotificationCategories"] = request.NotificationCategories;
                }
                if (request.DarkMode.HasValue)
                {
                    updates["DarkMode"] = request.DarkMode.Value;
                }
                if (request.AnonymousMode.HasValue)
                {
                    updates["AnonymousMode"] = request.AnonymousMode.Value;
                }
                if (request.TrustedContacts != null)
                {
                    if (!request.TrustedContacts.Any())
                    {
                        return Error("At least one trusted contact is required");
                    }
                    // FIXED: Validate and deduplicate trusted contacts
                    request.TrustedContacts = request.TrustedContacts.Distinct().ToList();
                    foreach (var contact in request.TrustedContacts)
                    {
                        if (!IsValidEmail(contact))
                        {
                            return Error($"Invalid trusted contact email: {contact}");
                        }
                    }
                    updates["TrustedContacts"] = request.TrustedContacts;
                }
                if (request.LocationSharingDuration.HasValue)
                {
                    if (!_validationService.IsValidDuration(request.LocationSharingDuration.Value))
                    {
                        return Error("Location sharing duration must be between 15 and 120 minutes");
                    }
                    updates["LocationSharingDuration"] = request.LocationSharingDuration.Value;
                }
                if (!string.IsNullOrEmpty(request.FCMToken))
                {
                    updates["FCMToken"] = request.FCMToken;
                }
                if (!updates.Any())
                {
                    return Error("No valid settings provided for update");
                }
                await _firebaseService.UpdateUserAsync(userId, updates);
                _logger.LogInformation("Settings updated successfully for user: {UserId}", userId);
                return Success(new { success = true, updatedFields = updates.Keys });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating user settings");
                return Error("Internal server error", 500);
            }
        }

        [Authorize]
        [HttpGet("profile")]
        public async Task<IActionResult> GetProfile()
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
                // Return safe profile data (exclude sensitive info)
                var profile = new
                {
                    user.UserId,
                    user.Email,
                    user.Suburb,
                    user.NotificationCategories,
                    user.DarkMode,
                    user.AnonymousMode,
                    user.LocationSharingDuration,
                    user.CreatedAt
                };
                return Success(profile);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting user profile");
                return Error("Internal server error", 500);
            }
        }

        [Authorize]
        [HttpGet("suburbs")]
        public IActionResult GetPESuburbs()
        {
            var suburbs = _validationService.GetPESuburbs();
            return Success(new { suburbs, count = suburbs.Count });
        }

        [Authorize]
        [HttpGet("categories")]
        public IActionResult GetCategories()
        {
            var categories = _validationService.GetValidCategories();
            return Success(new { categories, count = categories.Count });
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