using Google.Cloud.Firestore;
using System.ComponentModel.DataAnnotations;

namespace SentinelApi.Models
{
    [FirestoreData]
    public class CreateGroupRequest
    {
        [Required]
        public string Name { get; set; } = string.Empty;

        [Required]
        public string Suburb { get; set; } = string.Empty;
    }

    [FirestoreData]
    public class JoinGroupRequest
    {
        [Required]
        public string GroupId { get; set; } = string.Empty;
    }

    [FirestoreData]
    public class Group
    {
        [FirestoreProperty]
        public string GroupId { get; set; } = string.Empty;

        [FirestoreProperty]
        public string Name { get; set; } = string.Empty;

        [FirestoreProperty]
        public string Suburb { get; set; } = string.Empty;

        [FirestoreProperty]
        public string CreatedBy { get; set; } = string.Empty;

        [FirestoreProperty]
        public DateTime CreatedAt { get; set; }
    }
}