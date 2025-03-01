using CommunityPortal.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using CommunityPortal.Models;
using CommunityPortal.Models.Enums;
using CommunityPortal.Hubs;
using CommunityPortal.Services;
using CommunityPortal.Models.Documents;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection") ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(connectionString));

// Register PDF Service
builder.Services.AddScoped<PdfService>();

builder.Services.AddIdentity<ApplicationUser, IdentityRole>(options =>
{
    options.SignIn.RequireConfirmedAccount = false;
    options.Password.RequireDigit = false;
    options.Password.RequiredLength = 6; // Minimum length
    options.Password.RequireNonAlphanumeric = false;
    options.Password.RequireUppercase = false;
    options.Password.RequireLowercase = false;
})
    .AddRoles<IdentityRole>()
    .AddDefaultTokenProviders()
    .AddEntityFrameworkStores<ApplicationDbContext>();

// for policy
builder.Services.AddAuthorizationBuilder()
    .AddPolicy("AdminOnly", policy =>
    {
        policy.RequireRole("admin");
    });

//Access Denied
builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/Account/Login";
    options.AccessDeniedPath = "/Account/AccessDenied"; // Make sure this matches the view path
});

// Add services to the container.
builder.Services.AddControllersWithViews();

// Register Razor Pages so that MapRazorPages() can properly locate the required services.
builder.Services.AddRazorPages();

builder.Services.AddSignalR();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseAuthentication(); // Add Authentication
app.UseAuthorization();

app.MapHub<ChatHub>("/chatHub");


app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Landing}/{id?}");

// Initialize Roles and Admin User
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    var roleManager = services.GetRequiredService<RoleManager<IdentityRole>>();
    var userManager = services.GetRequiredService<UserManager<ApplicationUser>>();
    var context = services.GetRequiredService<ApplicationDbContext>();

    var roles = new[] { "admin", "staff", "homeowners" };

    foreach (var role in roles)
    {
        if (!await roleManager.RoleExistsAsync(role))
        {
            await roleManager.CreateAsync(new IdentityRole(role));
        }
    }

    // Create an admin user if it doesn't exist
    string adminEmail = "admin@gmail.com";
    string adminPassword = "123123"; // Use a strong password in production

    var adminUser = await userManager.FindByEmailAsync(adminEmail);
    if (adminUser == null)
    {
        adminUser = new ApplicationUser
        {
            UserName = adminEmail,
            Email = adminEmail,
            Status = UserStatus.Approved, // Admins are approved by default
            ProfileImagePath = "images/default-profile.jpg",
            PhoneNumber = "09123456789",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };


        var result = await userManager.CreateAsync(adminUser, adminPassword);
        if (result.Succeeded)
        {
            await userManager.AddToRoleAsync(adminUser, "admin");

            // Create Admin profile
            var adminProfile = new Administrator
            { 
                UserId = adminUser.Id,
                FirstName = "Admin",
                LastName = "Admin",
                Address = "123 Admin St, Admin City, Admin Country"
            };
            
            context.Admins.Add(adminProfile);
            await context.SaveChangesAsync();

            // Update document records with the actual admin ID
            var adminId = adminUser?.Id;
            if (!string.IsNullOrEmpty(adminId))
            {
                // First check if we need to seed documents
                var existingDocs = await context.Documents.AnyAsync();
                if (!existingDocs)
                {
                    // No documents exist yet, so seed them directly
                    var documents = new List<Document>
                    {
                        new Document
                        {
                            Title = "Billing and Payment Guidelines",
                            Description = "Guidelines for billing and payment procedures in the community",
                            FilePath = "images/default/Guidelines-Billing-Payment.pdf",
                            FileName = "Guidelines-Billing-Payment.pdf",
                            FileType = "application/pdf",
                            FileSizeInKB = 500,
                            Category = DocumentCategory.CommunityGuidelines,
                            UploadDate = DateTime.UtcNow,
                            LastUpdated = DateTime.UtcNow,
                            UploadedById = adminId,
                            IsDeleted = false
                        },
                        new Document
                        {
                            Title = "Facility Usage Guidelines",
                            Description = "Guidelines for using community facilities and amenities",
                            FilePath = "images/default/Guidelines-Facility.pdf",
                            FileName = "Guidelines-Facility.pdf",
                            FileType = "application/pdf",
                            FileSizeInKB = 450,
                            Category = DocumentCategory.CommunityGuidelines,
                            UploadDate = DateTime.UtcNow,
                            LastUpdated = DateTime.UtcNow,
                            UploadedById = adminId,
                            IsDeleted = false
                        },
                        new Document
                        {
                            Title = "Service Request and Maintenance Guidelines",
                            Description = "Guidelines for submitting and processing service and maintenance requests",
                            FilePath = "images/default/Guidelines-Service-Request-Maintenance.pdf",
                            FileName = "Guidelines-Service-Request-Maintenance.pdf",
                            FileType = "application/pdf",
                            FileSizeInKB = 525,
                            Category = DocumentCategory.CommunityGuidelines,
                            UploadDate = DateTime.UtcNow,
                            LastUpdated = DateTime.UtcNow,
                            UploadedById = adminId,
                            IsDeleted = false
                        },
                        new Document
                        {
                            Title = "Subdivision Rules and Regulations",
                            Description = "Official rules and regulations governing the subdivision",
                            FilePath = "images/default/Guidelines-Subdivision-Rules-1.pdf",
                            FileName = "Guidelines-Subdivision-Rules-1.pdf",
                            FileType = "application/pdf",
                            FileSizeInKB = 600,
                            Category = DocumentCategory.CommunityGuidelines,
                            UploadDate = DateTime.UtcNow,
                            LastUpdated = DateTime.UtcNow,
                            UploadedById = adminId,
                            IsDeleted = false
                        }
                    };

                    context.Documents.AddRange(documents);
                    await context.SaveChangesAsync();
                    Console.WriteLine($"Seeded {documents.Count} document records with admin ID");
                }
                else
                {
                    // Check for documents with placeholder ID and update them
                    var docsToUpdate = await context.Documents
                        .Where(d => d.UploadedById == "11111111-1111-1111-1111-111111111111")
                        .ToListAsync();
                        
                    if (docsToUpdate.Any())
                    {
                        foreach (var doc in docsToUpdate)
                        {
                            doc.UploadedById = adminId;
                        }
                        await context.SaveChangesAsync();
                        Console.WriteLine($"Updated {docsToUpdate.Count} document records with the correct admin ID");
                    }
                }
            }
        }
        else
        {
            // Handle creation errors
            throw new Exception("Failed to create admin user.");
        }
    }

    // Ensure all Staff users have the "staff" role assigned
    var staffUsers = await userManager.Users
        .Include(u => u.Staff)
        .Where(u => u.Staff != null)
        .ToListAsync();

    foreach (var staffUser in staffUsers)
    {
        if (!await userManager.IsInRoleAsync(staffUser, "staff"))
        {
            await userManager.AddToRoleAsync(staffUser, "staff");
            Console.WriteLine($"Assigned staff role to user {staffUser.Email}");
        }
    }
}

app.Run();
