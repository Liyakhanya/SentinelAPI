using Google.Cloud.Firestore;
using System.ComponentModel.DataAnnotations;
namespace SentinelApi.Models
{
    public class RegisterRequest
    {
        [Required, EmailAddress]
        public string Email { get; set; } = string.Empty;

        [Required, MinLength(6)]
        public string Password { get; set; } = string.Empty;

        [Required]
        public string Suburb { get; set; } = string.Empty;

        [Required, MinLength(1)]
        public List<string> TrustedContacts { get; set; } = new();

        public string? FCMToken { get; set; }
    }
    public class LoginRequest
    {
        [Required, EmailAddress]
        public string Email { get; set; } = string.Empty;

        [Required]
        public string Password { get; set; } = string.Empty;

        public string? FCMToken { get; set; }
    }
    public class SettingsRequest
    {
        public string? Suburb { get; set; }
        public List<string>? NotificationCategories { get; set; }
        public bool? DarkMode { get; set; }
        public bool? AnonymousMode { get; set; }
        public List<string>? TrustedContacts { get; set; }
        public int? LocationSharingDuration { get; set; }
        public string? FCMToken { get; set; }
    }
    [FirestoreData]
    public class User
    {
        [FirestoreProperty]
        public string UserId { get; set; } = string.Empty;

        [FirestoreProperty]
        public string Email { get; set; } = string.Empty;

        [FirestoreProperty]
        public string Suburb { get; set; } = string.Empty;

        [FirestoreProperty]
        public List<string> TrustedContacts { get; set; } = new();

        [FirestoreProperty]
        public List<string> NotificationCategories { get; set; } = new() { "Robbery", "GBV", "Hazard" };

        [FirestoreProperty]
        public bool DarkMode { get; set; } = false;

        [FirestoreProperty]
        public bool AnonymousMode { get; set; } = false;

        [FirestoreProperty]
        public List<string> Groups { get; set; } = new();

        [FirestoreProperty]
        public int LocationSharingDuration { get; set; } = 30;

        [FirestoreProperty]
        public string? FCMToken { get; set; }

        [FirestoreProperty]
        public DateTime CreatedAt { get; set; }

        [FirestoreProperty]
        public DateTime UpdatedAt { get; set; }

        // Add a constructor to ensure collections are initialized
        public User()
        {
            TrustedContacts = new List<string>();
            NotificationCategories = new List<string> { "Robbery", "GBV", "Hazard", "General" };
            Groups = new List<string>();
        }
    }
}