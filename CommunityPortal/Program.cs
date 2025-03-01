using CommunityPortal.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using CommunityPortal.Models;
using CommunityPortal.Models.Enums;
using CommunityPortal.Hubs;
using CommunityPortal.Services;
using CommunityPortal.Models.Documents;
using Microsoft.Extensions.Logging;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection") ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(connectionString));

// Register PDF Service
builder.Services.AddScoped<PdfService>();

// Register Notification Service
builder.Services.AddScoped<NotificationService>();

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
app.MapHub<NotificationHub>("/notificationHub");

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Landing}/{id?}");

// Add explicit routes for controllers that need them
app.MapControllerRoute(
    name: "chat",
    pattern: "Chat/{action=Index}/{id?}",
    defaults: new { controller = "Chat" });

app.MapControllerRoute(
    name: "forum",
    pattern: "Forum/{action=Index}/{id?}",
    defaults: new { controller = "Forum" });

app.MapControllerRoute(
    name: "billing",
    pattern: "Billing/{action=Index}/{id?}",
    defaults: new { controller = "Billing" });

app.MapControllerRoute(
    name: "event",
    pattern: "Event/{action=Index}/{id?}",
    defaults: new { controller = "Event" });

app.MapControllerRoute(
    name: "serviceRequest",
    pattern: "ServiceRequest/{action=Index}/{id?}",
    defaults: new { controller = "ServiceRequest" });

app.MapControllerRoute(
    name: "poll",
    pattern: "Poll/{action=Index}/{id?}",
    defaults: new { controller = "Poll" });

app.MapControllerRoute(
    name: "documents",
    pattern: "Documents/{action=Index}/{id?}",
    defaults: new { controller = "Documents" });

app.MapControllerRoute(
    name: "notifications",
    pattern: "Notifications/{action=Index}/{id?}",
    defaults: new { controller = "Notifications" });

app.MapControllerRoute(
    name: "facility",
    pattern: "Facility/{action=Index}/{id?}",
    defaults: new { controller = "Facility" });

// Initialize Roles and Admin User
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    var roleManager = services.GetRequiredService<RoleManager<IdentityRole>>();
    var userManager = services.GetRequiredService<UserManager<ApplicationUser>>();
    var context = services.GetRequiredService<ApplicationDbContext>();
    var logger = services.GetRequiredService<ILogger<Program>>();

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
                    logger.LogInformation($"Seeded {documents.Count} document records with admin ID");
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
                        logger.LogInformation($"Updated {docsToUpdate.Count} document records with the correct admin ID");
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

    // Create staff users at runtime
    var staffData = new[]
    {
        new { Email = "aiah@gmail.com", UserName = "aiah@gmail.com", Phone = "09772719114", Image = "images/default/Aiah.png", 
              FirstName = "Aiah", LastName = "Arceta", Department = "Maintenance Department", Position = "General Maintenance Worker", Address = "123 Main St, Cityville" },
        new { Email = "david@gmail.com", UserName = "david@gmail.com", Phone = "09772719114", Image = "images/default/David.png",
              FirstName = "David", LastName = "Guison", Department = "Maintenance Department", Position = "Plumber", Address = "456 Elm St, Townsville" },
        new { Email = "marinella@gmail.com", UserName = "marinella@gmail.com", Phone = "09772719114", Image = "images/default/Caber.png",
              FirstName = "Marinella", LastName = "Caber", Department = "Maintenance Department", Position = "Electrician", Address = "789 Oak St, Villageton" },
        new { Email = "kevin@gmail.com", UserName = "kevin@gmail.com", Phone = "09772719114", Image = "images/default/Tamayo.png",
              FirstName = "Kevin", LastName = "Tamayo", Department = "Maintenance Department", Position = "HVAC Technician", Address = "101 Pine St, Metrocity" },
        new { Email = "slater@gmail.com", UserName = "slater@gmail.com", Phone = "09772719114", Image = "images/default/Slater.png",
              FirstName = "Slater", LastName = "Young", Department = "Maintenance Department", Position = "Carpenter", Address = "202 Birch St, Suburbia" },
        new { Email = "crist@gmail.com", UserName = "crist@gmail.com", Phone = "09772719114", Image = "images/default/Brader.png",
              FirstName = "Crist", LastName = "Briand", Department = "Security Department", Position = "Security Guard", Address = "303 Cedar St, Uptown" },
        new { Email = "al@gmail.com", UserName = "al@gmail.com", Phone = "09772719114", Image = "images/default/Roblox.png",
              FirstName = "Al", LastName = "Moralde", Department = "Security Department", Position = "Access Control Officer", Address = "404 Redwood St, Downtown" },
        new { Email = "rowell@gmail.com", UserName = "rowell@gmail.com", Phone = "09772719114", Image = "images/default/Rowell.png",
              FirstName = "Rowell", LastName = "Divina", Department = "Housekeeping & Sanitation Department", Position = "Janitor/Cleaner", Address = "505 Maple St, Citytown" },
        new { Email = "hev@gmail.com", UserName = "hev@gmail.com", Phone = "09772719114", Image = "images/default/Hev.png",
              FirstName = "Hev", LastName = "Abi", Department = "Housekeeping & Sanitation Department", Position = "Waste Management Staff", Address = "606 Spruce St, Countryville" },
        new { Email = "denise@gmail.com", UserName = "denise@gmail.com", Phone = "09772719114", Image = "images/default/Denise.png",
              FirstName = "Denise", LastName = "Julia", Department = "Landscaping & Gardening Department", Position = "Gardener", Address = "707 Willow St, Riverside" }
    };

    logger.LogInformation("Checking staff users...");
    int createdStaffCount = 0;
    int existingStaffCount = 0;

    foreach (var staffInfo in staffData)
    {
        var staffUser = await userManager.FindByEmailAsync(staffInfo.Email);
        
        if (staffUser == null)
        {
            // Create the user
            staffUser = new ApplicationUser
            {
                UserName = staffInfo.UserName,
                Email = staffInfo.Email,
                Status = UserStatus.Approved,
                ProfileImagePath = staffInfo.Image,
                PhoneNumber = staffInfo.Phone,
                EmailConfirmed = true,
                PhoneNumberConfirmed = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            var result = await userManager.CreateAsync(staffUser, "123123");
            
            if (result.Succeeded)
            {
                // Assign staff role
                await userManager.AddToRoleAsync(staffUser, "staff");
                
                // Create staff profile
                var staffProfile = new Staff
                { 
                    UserId = staffUser.Id,
                    FirstName = staffInfo.FirstName,
                    LastName = staffInfo.LastName,
                    Department = staffInfo.Department,
                    Position = staffInfo.Position,
                    Address = staffInfo.Address
                };
                
                context.Staffs.Add(staffProfile);
                createdStaffCount++;
            }
            else 
            {
                logger.LogError($"Failed to create staff: {staffInfo.Email}, Errors: {string.Join(", ", result.Errors.Select(e => e.Description))}");
            }
        }
        else 
        {
            existingStaffCount++;
            
            // Ensure role is assigned if user exists but doesn't have the role
            if (!await userManager.IsInRoleAsync(staffUser, "staff"))
            {
                await userManager.AddToRoleAsync(staffUser, "staff");
                logger.LogInformation($"Assigned staff role to existing user: {staffInfo.Email}");
            }
            
            // Check if staff profile exists
            var existingProfile = await context.Staffs.FirstOrDefaultAsync(s => s.UserId == staffUser.Id);
            if (existingProfile == null)
            {
                // Create staff profile if missing
                var staffProfile = new Staff
                { 
                    UserId = staffUser.Id,
                    FirstName = staffInfo.FirstName,
                    LastName = staffInfo.LastName,
                    Department = staffInfo.Department,
                    Position = staffInfo.Position,
                    Address = staffInfo.Address
                };
                
                context.Staffs.Add(staffProfile);
                logger.LogInformation($"Created missing profile for staff user: {staffInfo.Email}");
            }
        }
    }
    
    if (createdStaffCount > 0 || existingStaffCount > 0)
    {
        await context.SaveChangesAsync();
        logger.LogInformation($"Staff seeding complete. Created {createdStaffCount} new staff, found {existingStaffCount} existing staff.");
    }
}

app.Run();
