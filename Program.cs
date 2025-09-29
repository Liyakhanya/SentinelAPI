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

            // Configure Firebase for both Development and Production
            var projectId = builder.Configuration["Firebase:ProjectId"];
            GoogleCredential credential;

            // Production environment handling
            if (builder.Environment.IsProduction())
            {
                Console.WriteLine("Running in PRODUCTION environment (Heroku)");

                // For Heroku, use Application Default Credentials
                try
                {
                    credential = GoogleCredential.GetApplicationDefault();
                    Console.WriteLine("Using Application Default Credentials for Heroku");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"ADC failed: {ex.Message}. Trying file-based...");

                    // Fallback to file if exists
                    var filePath = "sentinel-c2ba1-firebase-adminsdk-fbsvc-4ba80c21c1.json";
                    if (File.Exists(filePath))
                    {
                        credential = GoogleCredential.FromFile(filePath);
                        Console.WriteLine("Using file-based credentials");
                    }
                    else
                    {
                        throw new InvalidOperationException("No Firebase credentials found for production");
                    }
                }
            }
            else
            {
                // Development environment - use local file
                Console.WriteLine("Running in DEVELOPMENT environment");
                var credentialPath = builder.Configuration["Firebase:CredentialPath"];
                var fullPath = Path.GetFullPath(credentialPath ?? "");
                Console.WriteLine($"Checking credential file at: {fullPath}");

                if (!string.IsNullOrEmpty(credentialPath) && File.Exists(fullPath))
                {
                    credential = GoogleCredential.FromFile(fullPath);
                    Console.WriteLine($"Loaded Firebase credentials from {fullPath}");
                }
                else
                {
                    Console.WriteLine($"File not found at {fullPath}. Using Application Default Credentials...");
                    credential = GoogleCredential.GetApplicationDefault();
                }
            }

            // Initialize Firebase
            try
            {
                var firebaseApp = FirebaseApp.Create(new AppOptions()
                {
                    Credential = credential,
                    ProjectId = projectId
                });
                Console.WriteLine("FirebaseApp initialized successfully");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Firebase initialization warning: {ex.Message}");
                // Continue - Firebase might initialize later
            }

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

            app.UseCors("AllowAll");
            app.UseAuthentication();
            app.UseAuthorization();
            app.UseRateLimiter();
            app.MapControllers();

            // HEROKU SUPPORT: Use PORT environment variable
            var port = Environment.GetEnvironmentVariable("PORT") ?? "8080";
            var url = $"http://0.0.0.0:{port}";
            Console.WriteLine($"Starting API on: {url}");
            app.Run(url);
        }
    }
}