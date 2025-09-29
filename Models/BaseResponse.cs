using Google.Cloud.Firestore;

namespace SentinelApi.Models
{
    [FirestoreData]
    public class BaseResponse<T>
    {
        [FirestoreProperty]
        public bool Success { get; set; }

        [FirestoreProperty]
        public string? Message { get; set; }

        [FirestoreProperty]
        public T? Data { get; set; }

        [FirestoreProperty]
        public string? Error { get; set; }

        [FirestoreProperty]
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    }
}