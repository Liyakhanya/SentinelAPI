using Google.Cloud.Firestore;
using System.ComponentModel.DataAnnotations;

namespace SentinelApi.Models
{
    [FirestoreData]
    public class LocationShareRequest
    {
        [Required, MinLength(1)]
        public List<string> Contacts { get; set; } = new();

        [Required, Range(15, 120)]
        public int Duration { get; set; }

        [Required]
        public double Latitude { get; set; }

        [Required]
        public double Longitude { get; set; }
    }

    [FirestoreData]
    public class LocationShare
    {
        [FirestoreProperty]
        public string ShareId { get; set; } = string.Empty;

        [FirestoreProperty]
        public string UserId { get; set; } = string.Empty;

        [FirestoreProperty]
        public List<string> Contacts { get; set; } = new();

        [FirestoreProperty]
        public int Duration { get; set; }

        [FirestoreProperty]
        public GeoPoint? Location { get; set; }

        [FirestoreProperty]
        public DateTime CreatedAt { get; set; }

        [FirestoreProperty]
        public DateTime ExpiresAt { get; set; }

        public LocationShare()
        {
            Contacts = new List<string>();
        }
    }
}