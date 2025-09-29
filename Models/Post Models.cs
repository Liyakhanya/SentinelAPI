using Google.Cloud.Firestore;
using System.ComponentModel.DataAnnotations;

namespace SentinelApi.Models
{
    [FirestoreData]
    public class PostRequest
    {
        [Required]
        public string Title { get; set; } = string.Empty;

        public string? Description { get; set; }

        public string? Category { get; set; }

        [Required]
        public string Suburb { get; set; } = string.Empty;

        public bool Anonymous { get; set; } = false;

        public string? GroupId { get; set; }

        public string? MediaUrl { get; set; }

        public double? Latitude { get; set; }

        public double? Longitude { get; set; }
    }

    [FirestoreData]
    public class Post
    {
        [FirestoreProperty]
        public string PostId { get; set; } = string.Empty;

        [FirestoreProperty]
        public string? UserId { get; set; }

        [FirestoreProperty]
        public string Title { get; set; } = string.Empty;

        [FirestoreProperty]
        public string Description { get; set; } = string.Empty;

        [FirestoreProperty]
        public string Category { get; set; } = "General";

        [FirestoreProperty]
        public string Suburb { get; set; } = string.Empty;

        [FirestoreProperty]
        public string? GroupId { get; set; }

        [FirestoreProperty]
        public string? MediaUrl { get; set; }

        [FirestoreProperty]
        public GeoPoint? Location { get; set; }

        [FirestoreProperty]
        public DateTime CreatedAt { get; set; }
    }
}