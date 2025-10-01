using FirebaseAdmin;
using FirebaseAdmin.Auth;
using Google.Apis.Auth.OAuth2;
using Google.Cloud.Firestore;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using SentinelApi.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container
builder.Services.AddControllers();

// Configure Firebase
var projectId = builder.Configuration["Firebase:ProjectId"] ?? "sentinel-c2ba1";
GoogleCredential credential;

try
{
    Console.WriteLine("=== STARTING APPLICATION ===");
    Console.WriteLine($"Environment: {builder.Environment.EnvironmentName}");

    var firebaseJson = Environment.GetEnvironmentVariable("FIREBASE_CREDENTIALS_JSON");

    if (!string.IsNullOrEmpty(firebaseJson))
    {
        // Unescape the JSON - replace \n with actual newlines
        var unescapedJson = firebaseJson.Replace("\\n", "\n");
        credential = GoogleCredential.FromJson(unescapedJson);
        Console.WriteLine("✓ Loaded Firebase credentials from FIREBASE_CREDENTIALS_JSON environment variable");
    }
    else
    {
        // Fallback to local file
        var credentialFile = "sentinel-c2ba1-firebase-adminsdk-fbsvc-4ba80c21c1.json";
        if (!File.Exists(credentialFile))
        {
            throw new FileNotFoundException($"Firebase credentials file '{credentialFile}' not found");
        }
        credential = GoogleCredential.FromFile(credentialFile);
        Console.WriteLine($"✓ Loaded Firebase credentials from file: {credentialFile}");
    }

    FirebaseApp.Create(new AppOptions()
    {
        Credential = credential,
        ProjectId = projectId
    });
    Console.WriteLine("✓ Firebase initialized successfully");
}
catch (Exception ex)
{
    Console.WriteLine($"✗ Firebase initialization failed: {ex.Message}");
    throw;
}

// Initialize Firestore with lazy loading
builder.Services.AddSingleton(provider =>
{
    return new FirestoreDbBuilder
    {
        ProjectId = projectId,
        Credential = credential
    }.Build();
});

// Register services
builder.Services.AddSingleton(FirebaseAuth.DefaultInstance);
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

// CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

var app = builder.Build();


app.UseHttpsRedirection();
app.UseCors("AllowAll");
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.MapGet("/", () => "Sentinel API is running. Use Postman to test endpoints.");
app.MapGet("/health", () => Results.Ok(new { status = "Healthy", timestamp = DateTime.UtcNow }));

Console.WriteLine("=== APPLICATION STARTED SUCCESSFULLY ===");
app.Run();