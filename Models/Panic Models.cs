using Google.Cloud.Firestore;
using System.ComponentModel.DataAnnotations;

namespace SentinelApi.Models
{
    [FirestoreData]
    public class PanicRequest
    {
        [Required]
        public string Message { get; set; } = string.Empty;

        public double? Latitude { get; set; }

        public double? Longitude { get; set; }
    }

    [FirestoreData]
    public class PanicAlert
    {
        [FirestoreProperty]
        public string PanicId { get; set; } = string.Empty;

        [FirestoreProperty]
        public string UserId { get; set; } = string.Empty;

        [FirestoreProperty]
        public string Message { get; set; } = string.Empty;

        [FirestoreProperty]
        public GeoPoint? Location { get; set; }

        [FirestoreProperty]
        public DateTime CreatedAt { get; set; }
    }
}