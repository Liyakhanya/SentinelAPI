using Google.Cloud.Firestore;
using SentinelApi.Models;

namespace SentinelApi.Services
{
    public interface IFirebaseService
    {
        Task<User?> GetUserAsync(string userId);
        Task<bool> UserExistsInGroupAsync(string userId, string groupId);
        Task<List<User>> GetUsersBySuburbAsync(string suburb);
        Task<List<User>> GetUsersByGroupAsync(string groupId);
        Task<List<User>> GetUsersByEmailsAsync(List<string> emails);
        Task<List<User>> GetUsersByNotificationCategoryAsync(string suburb, string category);
        Task<string> CreateUserAsync(User user);
        Task<bool> UpdateUserAsync(string userId, Dictionary<string, object> updates);
        Task<string> CreatePostAsync(Post post);
        Task<string> CreateGroupAsync(Group group);
        Task<Group?> GetGroupAsync(string groupId);
        Task<string> CreatePanicAlertAsync(PanicAlert alert);
        Task<string> CreateLocationShareAsync(LocationShare share);
        Task<List<Post>> GetPostsAsync(string? suburb, string? groupId = null, string? category = null, int limit = 50);
        Task<List<Post>> GetPostsByLocationAsync(double latitude, double longitude, double radiusInKm, string? category = null);
        Task<List<Post>> GetGroupPostsAsync(string groupId);
        Task<List<Group>> GetGroupsBySuburbAsync(string suburb);
        Task<List<PanicAlert>> GetRecentPanicAlertsAsync(string userId, int hours = 24);
        Task<bool> DeleteExpiredLocationSharesAsync();
        FirestoreDb GetFirestoreDb();
    }

    public class FirebaseService : IFirebaseService
    {
        private readonly FirestoreDb _firestoreDb;
        private readonly ILogger<FirebaseService> _logger;

        public FirebaseService(FirestoreDb firestoreDb, ILogger<FirebaseService> logger)
        {
            _firestoreDb = firestoreDb;
            _logger = logger;
        }

        public FirestoreDb GetFirestoreDb() => _firestoreDb;

        public async Task<User?> GetUserAsync(string userId)
        {
            try
            {
                var doc = await _firestoreDb.Collection("users").Document(userId).GetSnapshotAsync();
                if (doc.Exists)
                {
                    var user = doc.ConvertTo<User>();
                    user.UserId = userId;
                    return user;
                }
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting user {UserId}", userId);
                throw;
            }
        }

        public async Task<bool> UserExistsInGroupAsync(string userId, string groupId)
        {
            var user = await GetUserAsync(userId);
            return user?.Groups?.Contains(groupId) == true;
        }

        public async Task<List<User>> GetUsersBySuburbAsync(string suburb)
        {
            try
            {
                var query = _firestoreDb.Collection("users").WhereEqualTo("Suburb", suburb);
                var snapshot = await query.GetSnapshotAsync();
                return snapshot.Documents.Select(doc =>
                {
                    var user = doc.ConvertTo<User>();
                    user.UserId = doc.Id;
                    return user;
                }).ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting users by suburb {Suburb}", suburb);
                throw;
            }
        }

        public async Task<List<User>> GetUsersByGroupAsync(string groupId)
        {
            try
            {
                var query = _firestoreDb.Collection("users").WhereArrayContains("Groups", groupId);
                var snapshot = await query.GetSnapshotAsync();
                return snapshot.Documents.Select(doc =>
                {
                    var user = doc.ConvertTo<User>();
                    user.UserId = doc.Id;
                    return user;
                }).ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting users by group {GroupId}", groupId);
                throw;
            }
        }

        public async Task<List<User>> GetUsersByEmailsAsync(List<string> emails)
        {
            try
            {
                var users = new List<User>();
                var uniqueEmails = emails.Distinct().ToList();
                var chunks = uniqueEmails.Chunk(10);
                foreach (var chunk in chunks)
                {
                    var query = _firestoreDb.Collection("users").WhereIn("Email", chunk.ToList());
                    var snapshot = await query.GetSnapshotAsync();
                    users.AddRange(snapshot.Documents.Select(doc =>
                    {
                        var user = doc.ConvertTo<User>();
                        user.UserId = doc.Id;
                        return user;
                    }));
                }
                return users;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting users by emails");
                throw;
            }
        }

        public async Task<List<User>> GetUsersByNotificationCategoryAsync(string suburb, string category)
        {
            try
            {
                var query = _firestoreDb.Collection("users")
                    .WhereEqualTo("Suburb", suburb)
                    .WhereArrayContains("NotificationCategories", category);
                var snapshot = await query.GetSnapshotAsync();
                return snapshot.Documents.Select(doc =>
                {
                    var user = doc.ConvertTo<User>();
                    user.UserId = doc.Id;
                    return user;
                }).ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting users by notification category {Category} in {Suburb}", category, suburb);
                throw;
            }
        }

        public async Task<string> CreateUserAsync(User user)
        {
            try
            {
                var docRef = _firestoreDb.Collection("users").Document(user.UserId);
                await docRef.SetAsync(user);
                _logger.LogInformation("User created: {UserId}", user.UserId);
                return user.UserId;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating user");
                throw;
            }
        }

        public async Task<bool> UpdateUserAsync(string userId, Dictionary<string, object> updates)
        {
            try
            {
                updates["UpdatedAt"] = DateTime.UtcNow;
                var docRef = _firestoreDb.Collection("users").Document(userId);
                await docRef.UpdateAsync(updates);
                _logger.LogInformation("User updated: {UserId}", userId);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating user {UserId}", userId);
                throw;
            }
        }

        public async Task<string> CreatePostAsync(Post post)
        {
            try
            {
                var docRef = _firestoreDb.Collection("posts").Document();
                post.PostId = docRef.Id;
                post.CreatedAt = DateTime.UtcNow;
                await docRef.SetAsync(post);
                _logger.LogInformation("Post created: {PostId}", post.PostId);
                return post.PostId;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating post");
                throw;
            }
        }

        public async Task<string> CreateGroupAsync(Group group)
        {
            try
            {
                var docRef = _firestoreDb.Collection("groups").Document();
                group.GroupId = docRef.Id;
                group.CreatedAt = DateTime.UtcNow;
                await docRef.SetAsync(group);
                _logger.LogInformation("Group created: {GroupId}", group.GroupId);
                return group.GroupId;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating group");
                throw;
            }
        }

        public async Task<Group?> GetGroupAsync(string groupId)
        {
            try
            {
                var doc = await _firestoreDb.Collection("groups").Document(groupId).GetSnapshotAsync();
                if (doc.Exists)
                {
                    var group = doc.ConvertTo<Group>();
                    group.GroupId = groupId;
                    return group;
                }
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting group {GroupId}", groupId);
                throw;
            }
        }

        public async Task<string> CreatePanicAlertAsync(PanicAlert alert)
        {
            try
            {
                var docRef = _firestoreDb.Collection("panicAlerts").Document();
                alert.PanicId = docRef.Id;
                alert.CreatedAt = DateTime.UtcNow;
                await docRef.SetAsync(alert);
                _logger.LogInformation("Panic alert created: {PanicId}", alert.PanicId);
                return alert.PanicId;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating panic alert");
                throw;
            }
        }

        public async Task<string> CreateLocationShareAsync(LocationShare share)
        {
            try
            {
                var docRef = _firestoreDb.Collection("locationShares").Document();
                share.ShareId = docRef.Id;
                share.CreatedAt = DateTime.UtcNow;
                share.ExpiresAt = DateTime.UtcNow.AddMinutes(share.Duration);
                await docRef.SetAsync(share);
                _logger.LogInformation("Location share created: {ShareId}", share.ShareId);
                return share.ShareId;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating location share");
                throw;
            }
        }

        public async Task<List<Post>> GetPostsAsync(string? suburb, string? groupId = null, string? category = null, int limit = 50)
        {
            try
            {
                Query query = _firestoreDb.Collection("posts")
                    .OrderByDescending("CreatedAt")
                    .Limit(limit);

                var cutoff = DateTime.UtcNow.AddDays(-7);
                query = query.WhereGreaterThan("CreatedAt", cutoff);

                if (!string.IsNullOrEmpty(groupId))
                {
                    query = query.WhereEqualTo("GroupId", groupId);
                }
                else
                {
                    if (!string.IsNullOrEmpty(category))
                    {
                        query = query.WhereEqualTo("Category", category);
                    }
                    if (!string.IsNullOrEmpty(suburb))
                    {
                        query = query.WhereEqualTo("Suburb", suburb);
                    }
                }

                var snapshot = await query.GetSnapshotAsync();
                return snapshot.Documents.Select(doc =>
                {
                    var post = doc.ConvertTo<Post>();
                    post.PostId = doc.Id;
                    return post;
                }).ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting posts for suburb {Suburb}", suburb);
                throw;
            }
        }

        public async Task<List<Post>> GetPostsByLocationAsync(double latitude, double longitude, double radiusInKm, string? category = null)
        {
            try
            {
                var allPosts = await GetPostsAsync(null, null, category, 1000);
                var center = new GeoPoint(latitude, longitude);

                return allPosts
                    .Where(post => post.Location.HasValue && CalculateDistance(center, post.Location.Value) <= radiusInKm)
                    .ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting posts by location");
                throw;
            }
        }

        public async Task<List<Post>> GetGroupPostsAsync(string groupId)
        {
            try
            {
                var query = _firestoreDb.Collection("posts")
                    .WhereEqualTo("GroupId", groupId)
                    .OrderByDescending("CreatedAt")
                    .Limit(50);
                var snapshot = await query.GetSnapshotAsync();
                return snapshot.Documents.Select(doc =>
                {
                    var post = doc.ConvertTo<Post>();
                    post.PostId = doc.Id;
                    return post;
                }).ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting group posts for {GroupId}", groupId);
                throw;
            }
        }

        public async Task<List<Group>> GetGroupsBySuburbAsync(string suburb)
        {
            try
            {
                var query = _firestoreDb.Collection("groups").WhereEqualTo("Suburb", suburb);
                var snapshot = await query.GetSnapshotAsync();
                return snapshot.Documents.Select(doc =>
                {
                    var group = doc.ConvertTo<Group>();
                    group.GroupId = doc.Id;
                    return group;
                }).ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting groups for suburb {Suburb}", suburb);
                throw;
            }
        }

        public async Task<List<PanicAlert>> GetRecentPanicAlertsAsync(string userId, int hours = 24)
        {
            try
            {
                var cutoffTime = DateTime.UtcNow.AddHours(-hours);
                var query = _firestoreDb.Collection("panicAlerts")
                    .WhereEqualTo("UserId", userId)
                    .WhereGreaterThan("CreatedAt", cutoffTime)
                    .OrderByDescending("CreatedAt");
                var snapshot = await query.GetSnapshotAsync();
                return snapshot.Documents.Select(doc =>
                {
                    var alert = doc.ConvertTo<PanicAlert>();
                    alert.PanicId = doc.Id;
                    return alert;
                }).ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting recent panic alerts for user {UserId}", userId);
                throw;
            }
        }

        public async Task<bool> DeleteExpiredLocationSharesAsync()
        {
            try
            {
                var expiredTime = DateTime.UtcNow;
                var query = _firestoreDb.Collection("locationShares")
                    .WhereLessThan("ExpiresAt", expiredTime);
                var snapshot = await query.GetSnapshotAsync();
                var batch = _firestoreDb.StartBatch();
                foreach (var doc in snapshot.Documents)
                {
                    batch.Delete(doc.Reference);
                }
                await batch.CommitAsync();
                _logger.LogInformation("Deleted {Count} expired location shares", snapshot.Documents.Count);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting expired location shares");
                return false;
            }
        }

        private double CalculateDistance(GeoPoint point1, GeoPoint point2)
        {
            var R = 6371; // Earth's radius in km

            // FIXED: These are fine because point1 and point2 are non-nullable GeoPoint
            var dLat = ToRadians(point2.Latitude - point1.Latitude);
            var dLon = ToRadians(point2.Longitude - point1.Longitude);

            var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                    Math.Cos(ToRadians(point1.Latitude)) * Math.Cos(ToRadians(point2.Latitude)) *
                    Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
            var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
            return R * c;
        }

        private double ToRadians(double angle) => angle * (Math.PI / 180);
    }
}