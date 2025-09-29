using FirebaseAdmin;
using FirebaseAdmin.Auth;
using Google.Apis.Auth.OAuth2;
using Google.Cloud.Firestore;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.IdentityModel.Tokens;
using SentinelApi.Services;
using System.Threading.RateLimiting;

namespace SentinelApi
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);
            // Add services to the container
            builder.Services.AddControllers();
            builder.Services.AddEndpointsApiExplorer();
            builder.Services.AddSwaggerGen();
            // Configure Firebase
            var projectId = builder.Configuration["Firebase:ProjectId"];
            GoogleCredential credential;
            var credentialPath = builder.Configuration["Firebase:CredentialPath"];
            var fullPath = Path.GetFullPath(credentialPath ?? "");
            Console.WriteLine($"Current working directory: {Directory.GetCurrentDirectory()}");
            Console.WriteLine($"Checking credential file at: {fullPath}");
            if (!string.IsNullOrEmpty(credentialPath) && File.Exists(fullPath))
            {
                credential = GoogleCredential.FromFile(fullPath);
                Console.WriteLine($"Loaded Firebase credentials from {fullPath}");
            }
            else
            {
                Console.WriteLine($"File not found at {fullPath}. Attempting to use Application Default Credentials...");
                try
                {
                    credential = GoogleCredential.GetApplicationDefault();
                    Console.WriteLine("Using Application Default Credentials");
                }
                catch (Exception ex)
                {
                    throw new InvalidOperationException($"Firebase credentials not found. File not found at {fullPath}. Error with ADC: {ex.Message}. Please ensure the file exists or set up Application Default Credentials (see https://cloud.google.com/docs/authentication/external/set-up-adc).");
                }
            }
            var firebaseApp = FirebaseApp.Create(new AppOptions()
            {
                Credential = credential,
                ProjectId = projectId
            });
            Console.WriteLine("FirebaseApp initialized successfully");
            // Use FirestoreDbBuilder to create FirestoreDb
            var firestoreDb = new FirestoreDbBuilder
            {
                ProjectId = projectId,
                Credential = credential
            }.Build();
            // Register Firebase services
            builder.Services.AddSingleton(firestoreDb);
            builder.Services.AddSingleton(FirebaseAuth.DefaultInstance);
            // Register custom services
            builder.Services.AddScoped<IFirebaseService, FirebaseService>();
            builder.Services.AddScoped<IFCMService, FCMService>();
            builder.Services.AddScoped<IValidationService, ValidationService>();
            // Configure JWT authentication
            builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
                .AddJwtBearer(options =>
                {
                    options.Authority = $"https://securetoken.google.com/{projectId}";
                    options.TokenValidationParameters = new TokenValidationParameters
                    {
                        ValidateIssuer = true,
                        ValidIssuer = $"https://securetoken.google.com/{projectId}",
                        ValidateAudience = true,
                        ValidAudience = projectId,
                        ValidateLifetime = true
                    };
                });
            builder.Services.AddAuthorization();
            // Add rate limiting middleware from config
            builder.Services.AddRateLimiter(options =>
            {
                options.GlobalLimiter = PartitionedRateLimiter.Create<Microsoft.AspNetCore.Http.HttpContext, string>(httpContext =>
                    RateLimitPartition.GetFixedWindowLimiter(
                        partitionKey: httpContext.Request.Headers["X-Forwarded-For"].ToString() ?? httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                        factory: _ => new FixedWindowRateLimiterOptions
                        {
                            PermitLimit = 100,
                            Window = TimeSpan.FromMinutes(1)
                        }));
            });
            // Add CORS
            builder.Services.AddCors(options =>
            {
                options.AddPolicy("AllowAll", policy =>
                {
                    policy.AllowAnyOrigin()
                          .AllowAnyMethod()
                          .AllowAnyHeader();
                });
            });
            // Add logging
            builder.Services.AddLogging();
            var app = builder.Build();
            // Configure the HTTP request pipeline
            if (app.Environment.IsDevelopment())
            {
                app.UseSwagger();
                app.UseSwaggerUI();
            }
            app.UseHttpsRedirection();
            app.UseCors("AllowAll");
            app.UseAuthentication();
            app.UseAuthorization();
            app.UseRateLimiter();
            app.MapControllers();
            app.Run();
        }
    }
}