using FirebaseAdmin;
using FirebaseAdmin.Auth;
using Google.Apis.Auth.OAuth2;
using Google.Cloud.Firestore;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.IdentityModel.Tokens;
using SentinelApi.Services;
using System.Threading.RateLimiting;
using Microsoft.OpenApi.Models;

namespace SentinelApi
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

           
            builder.Services.AddControllers();
            builder.Services.AddEndpointsApiExplorer();
            builder.Services.AddSwaggerGen(c =>
            {
                c.SwaggerDoc("v1", new OpenApiInfo
                {
                    Title = "Sentinel API",
                    Version = "v1",
                    Description = "Community Safety API for Sentinel App"
                });

                
                c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
                {
                    Description = "JWT Authorization header using the Bearer scheme. Example: \"Authorization: Bearer {token}\"",
                    Name = "Authorization",
                    In = ParameterLocation.Header,
                    Type = SecuritySchemeType.ApiKey,
                    Scheme = "Bearer"
                });

                c.AddSecurityRequirement(new OpenApiSecurityRequirement
                {
                    {
                        new OpenApiSecurityScheme
                        {
                            Reference = new OpenApiReference
                            {
                                Type = ReferenceType.SecurityScheme,
                                Id = "Bearer"
                            }
                        },
                        new string[] {}
                    }
                });
            });

            
            var projectId = builder.Configuration["Firebase:ProjectId"];
            GoogleCredential credential;

            
            if (builder.Environment.IsProduction())
            {
                Console.WriteLine("Running in PRODUCTION environment");

                
                var envCredentialPath = Environment.GetEnvironmentVariable("FIREBASE_CONFIG_PATH");
                if (!string.IsNullOrEmpty(envCredentialPath) && File.Exists(envCredentialPath))
                {
                    credential = GoogleCredential.FromFile(envCredentialPath);
                    Console.WriteLine($"Loaded Firebase credentials from environment path: {envCredentialPath}");
                }
                else if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("GOOGLE_APPLICATION_CREDENTIALS")))
                {
                    credential = GoogleCredential.GetApplicationDefault();
                    Console.WriteLine("Using Application Default Credentials from GOOGLE_APPLICATION_CREDENTIALS");
                }
                
                else
                {
                    
                    credential = GoogleCredential.GetApplicationDefault();
                    Console.WriteLine("Using Application Default Credentials fallback");
                }
            }
            else
            {
                
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
                
            }

           
            var firestoreDb = new FirestoreDbBuilder
            {
                ProjectId = projectId,
                Credential = credential
            }.Build();

            
            builder.Services.AddSingleton(firestoreDb);
            builder.Services.AddSingleton(FirebaseAuth.DefaultInstance);

           
            builder.Services.AddScoped<IFirebaseService, FirebaseService>();
            builder.Services.AddScoped<IFCMService, FCMService>();
            builder.Services.AddScoped<IValidationService, ValidationService>();

           
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

            
            builder.Services.AddCors(options =>
            {
                options.AddPolicy("AllowAll", policy =>
                {
                    policy.AllowAnyOrigin()
                          .AllowAnyMethod()
                          .AllowAnyHeader();
                });

                
                // options.AddPolicy("Production", policy =>
                // {
                //     policy.WithOrigins("https://yourapp.com")
                //           .AllowAnyMethod()
                //           .AllowAnyHeader();
                // });
            });

            // Add logging
            builder.Services.AddLogging();

            var app = builder.Build();

            // Configure the HTTP request pipeline
            if (app.Environment.IsDevelopment())
            {
                app.UseSwagger();
                app.UseSwaggerUI(c =>
                {
                    c.SwaggerEndpoint("/swagger/v1/swagger.json", "Sentinel API v1");
                    c.RoutePrefix = "swagger";
                });
                app.UseCors("AllowAll");
            }
            else
            {
                app.UseCors("AllowAll"); 
                app.UseHttpsRedirection();

                
                app.UseSwagger();
                app.UseSwaggerUI(c =>
                {
                    c.SwaggerEndpoint("/swagger/v1/swagger.json", "Sentinel API v1");
                    c.RoutePrefix = "swagger";
                });
            }

            app.UseAuthentication();
            app.UseAuthorization();
            app.UseRateLimiter();
            app.MapControllers();

            app.Run();
        }
    }
}