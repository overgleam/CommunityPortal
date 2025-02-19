using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using CommunityPortal.Data;
using CommunityPortal.Models;
using CommunityPortal.Models.Forum;
using System.Security.Claims;

namespace CommunityPortal.Controllers
{
    [Authorize]
    public class ForumController : Controller
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ApplicationDbContext _context;
        private readonly IWebHostEnvironment _environment;

        public ForumController(
            UserManager<ApplicationUser> userManager,
            ApplicationDbContext context,
            IWebHostEnvironment environment)
        {
            _userManager = userManager;
            _context = context;
            _environment = environment;
        }

        // GET: /Forum
        public async Task<IActionResult> Index()
        {
            var user = await _userManager.GetUserAsync(User);
            var isAdmin = await _userManager.IsInRoleAsync(user, "admin");
            var isStaff = await _userManager.IsInRoleAsync(user, "staff");
            
            ViewBag.IsAdmin = isAdmin;
            ViewBag.IsStaff = isStaff;

            var posts = await _context.ForumPosts
                .Include(p => p.Author)
                .Include(p => p.Likes)
                .Include(p => p.Comments)
                    .ThenInclude(c => c.Author)
                .Include(p => p.Comments)
                    .ThenInclude(c => c.Likes)
                .Include(p => p.Comments)
                    .ThenInclude(c => c.Replies)
                        .ThenInclude(r => r.Author)
                .Include(p => p.Comments)
                    .ThenInclude(c => c.Replies)
                        .ThenInclude(r => r.Likes)
                .OrderByDescending(p => p.CreatedAt)
                .ToListAsync();

            // Get roles for all users in the posts
            var userRoles = new Dictionary<string, string>();
            foreach (var post in posts)
            {
                if (post.Author != null && !userRoles.ContainsKey(post.Author.Id))
                {
                    var roles = await _userManager.GetRolesAsync(post.Author);
                    userRoles[post.Author.Id] = roles.FirstOrDefault()?.ToUpper() ?? "";
                }

                foreach (var comment in post.Comments)
                {
                    if (comment.Author != null && !userRoles.ContainsKey(comment.Author.Id))
                    {
                        var roles = await _userManager.GetRolesAsync(comment.Author);
                        userRoles[comment.Author.Id] = roles.FirstOrDefault()?.ToUpper() ?? "";
                    }

                    foreach (var reply in comment.Replies)
                    {
                        if (reply.Author != null && !userRoles.ContainsKey(reply.Author.Id))
                        {
                            var roles = await _userManager.GetRolesAsync(reply.Author);
                            userRoles[reply.Author.Id] = roles.FirstOrDefault()?.ToUpper() ?? "";
                        }
                    }
                }
            }

            ViewBag.UserRoles = userRoles;
            return View(posts);
        }

        // GET: /Forum/Create
        public IActionResult Create()
        {
            return View();
        }

        // POST: /Forum/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(ForumPost post, IFormFile? image)
        {
            try
            {
                // Remove AuthorId from ModelState since we'll set it ourselves
                ModelState.Remove("Author");
                ModelState.Remove("AuthorId");

                if (!ModelState.IsValid)
                {
                    var errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage);
                    return View(post);
                }

                var user = await _userManager.GetUserAsync(User);
                if (user == null)
                {
                    return Challenge();
                }

                post.AuthorId = user.Id;
                post.CreatedAt = DateTime.UtcNow;
                post.UpdatedAt = DateTime.UtcNow;
                post.Comments = new List<ForumComment>();
                post.Likes = new List<ForumLike>();

                if (image != null)
                {
                    var uniqueFileName = Guid.NewGuid().ToString() + "_" + image.FileName;
                    var uploadsFolder = Path.Combine(_environment.WebRootPath, "uploads", "forum");
                    Directory.CreateDirectory(uploadsFolder);
                    var filePath = Path.Combine(uploadsFolder, uniqueFileName);

                    using (var fileStream = new FileStream(filePath, FileMode.Create))
                    {
                        await image.CopyToAsync(fileStream);
                    }

                    post.ImagePath = "/uploads/forum/" + uniqueFileName;
                }

                _context.ForumPosts.Add(post);
                await _context.SaveChangesAsync();

                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                // Log the exception
                ModelState.AddModelError("", "An error occurred while creating the post. Please try again.");
                return View(post);
            }
        }

        // GET: /Forum/Edit/5
        public async Task<IActionResult> Edit(int id)
        {
            var post = await _context.ForumPosts
                .Include(p => p.Author)
                .FirstOrDefaultAsync(p => p.Id == id);

            if (post == null)
            {
                TempData["ErrorMessage"] = "Post not found.";
                return RedirectToAction(nameof(Index));
            }

            var user = await _userManager.GetUserAsync(User);
            if (post.AuthorId != user.Id && !await _userManager.IsInRoleAsync(user, "admin"))
            {
                TempData["ErrorMessage"] = "You don't have permission to edit this post.";
                return RedirectToAction(nameof(Index));
            }

            return View(post);
        }

        // POST: /Forum/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, ForumPost post, IFormFile? image)
        {
            if (id != post.Id)
            {
                TempData["ErrorMessage"] = "Invalid post ID.";
                return RedirectToAction(nameof(Index));
            }

            // Remove Author from ModelState since we'll preserve it from the existing post
            ModelState.Remove("Author");
            ModelState.Remove("AuthorId");

            var existingPost = await _context.ForumPosts.FindAsync(id);
            if (existingPost == null)
            {
                TempData["ErrorMessage"] = "Post not found.";
                return RedirectToAction(nameof(Index));
            }

            var user = await _userManager.GetUserAsync(User);
            if (existingPost.AuthorId != user.Id && !await _userManager.IsInRoleAsync(user, "admin"))
            {
                TempData["ErrorMessage"] = "You don't have permission to edit this post.";
                return RedirectToAction(nameof(Index));
            }

            try
            {
                if (ModelState.IsValid)
                {
                    if (image != null)
                    {
                        var uniqueFileName = Guid.NewGuid().ToString() + "_" + image.FileName;
                        var uploadsFolder = Path.Combine(_environment.WebRootPath, "uploads", "forum");
                        Directory.CreateDirectory(uploadsFolder);
                        var filePath = Path.Combine(uploadsFolder, uniqueFileName);

                        using (var fileStream = new FileStream(filePath, FileMode.Create))
                        {
                            await image.CopyToAsync(fileStream);
                        }

                        // Delete old image if exists
                        if (!string.IsNullOrEmpty(existingPost.ImagePath))
                        {
                            var oldFilePath = Path.Combine(_environment.WebRootPath, existingPost.ImagePath.TrimStart('/'));
                            if (System.IO.File.Exists(oldFilePath))
                            {
                                System.IO.File.Delete(oldFilePath);
                            }
                        }

                        existingPost.ImagePath = "/uploads/forum/" + uniqueFileName;
                    }

                    existingPost.Title = post.Title;
                    existingPost.Content = post.Content;
                    existingPost.UpdatedAt = DateTime.UtcNow;

                    await _context.SaveChangesAsync();
                    TempData["SuccessMessage"] = "Post updated successfully.";
                    return RedirectToAction(nameof(Index));
                }
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", "An error occurred while updating the post.");
                // Log the exception
            }

            // If we got this far, something failed, redisplay form
            return View(post);
        }

        // POST: /Forum/Delete/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            try
            {
                var post = await _context.ForumPosts
                    .Include(p => p.Likes)
                    .Include(p => p.Comments)
                        .ThenInclude(c => c.Likes)
                    .Include(p => p.Comments)
                        .ThenInclude(c => c.Replies)
                            .ThenInclude(r => r.Likes)
                    .FirstOrDefaultAsync(p => p.Id == id);

                if (post == null)
                {
                    TempData["ErrorMessage"] = "Post not found.";
                    return RedirectToAction(nameof(Index));
                }

                var user = await _userManager.GetUserAsync(User);
                var isAdmin = await _userManager.IsInRoleAsync(user, "admin");
                
                // Only post author or admin can delete
                if (post.AuthorId != user.Id && !isAdmin)
                {
                    TempData["ErrorMessage"] = "You don't have permission to delete this post.";
                    return RedirectToAction(nameof(Index));
                }

                // Delete all associated likes (post, comments, and replies)
                _context.ForumLikes.RemoveRange(post.Likes);
                
                foreach (var comment in post.Comments)
                {
                    _context.ForumLikes.RemoveRange(comment.Likes);
                    
                    foreach (var reply in comment.Replies)
                    {
                        _context.ForumLikes.RemoveRange(reply.Likes);
                    }
                    
                    // Delete all replies
                    _context.ForumComments.RemoveRange(comment.Replies);
                }
                
                // Delete all comments
                _context.ForumComments.RemoveRange(post.Comments);

                // Delete the image if it exists
                if (!string.IsNullOrEmpty(post.ImagePath))
                {
                    var filePath = Path.Combine(_environment.WebRootPath, post.ImagePath.TrimStart('/'));
                    if (System.IO.File.Exists(filePath))
                    {
                        System.IO.File.Delete(filePath);
                    }
                }

                // Finally, delete the post
                _context.ForumPosts.Remove(post);
                await _context.SaveChangesAsync();

                TempData["SuccessMessage"] = "Post deleted successfully.";
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = "An error occurred while deleting the post.";
                // Log the exception
            }

            return RedirectToAction(nameof(Index));
        }

        // POST: /Forum/Comment
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Comment(int postId, string content, int? parentCommentId)
        {
            var post = await _context.ForumPosts.FindAsync(postId);
            if (post == null)
            {
                return NotFound();
            }

            var user = await _userManager.GetUserAsync(User);
            var comment = new ForumComment
            {
                Content = content,
                PostId = postId,
                AuthorId = user.Id,
                ParentCommentId = parentCommentId
            };

            _context.ForumComments.Add(comment);
            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(Index));
        }

        // POST: /Forum/DeleteComment/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteComment(int id)
        {
            var comment = await _context.ForumComments
                .Include(c => c.Likes)
                .Include(c => c.Replies)
                    .ThenInclude(r => r.Likes)
                .FirstOrDefaultAsync(c => c.Id == id);

            if (comment == null)
            {
                return NotFound();
            }

            var user = await _userManager.GetUserAsync(User);
            var isAdmin = await _userManager.IsInRoleAsync(user, "admin");

            // Only comment author or admin can delete
            if (comment.AuthorId != user.Id && !isAdmin)
            {
                return Forbid();
            }

            try
            {
                // Delete likes on the comment
                _context.ForumLikes.RemoveRange(comment.Likes);

                // Delete likes on replies and the replies themselves
                foreach (var reply in comment.Replies)
                {
                    _context.ForumLikes.RemoveRange(reply.Likes);
                }
                _context.ForumComments.RemoveRange(comment.Replies);

                // Delete the comment
                _context.ForumComments.Remove(comment);
                await _context.SaveChangesAsync();

                TempData["SuccessMessage"] = "Comment deleted successfully.";
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = "An error occurred while deleting the comment.";
                // Log the exception
            }

            return RedirectToAction(nameof(Index));
        }

        // POST: /Forum/Like
        [HttpPost]
        public async Task<IActionResult> Like(int? postId, int? commentId)
        {
            if (!postId.HasValue && !commentId.HasValue)
            {
                return BadRequest();
            }

            var user = await _userManager.GetUserAsync(User);
            var existingLike = await _context.ForumLikes
                .FirstOrDefaultAsync(l => l.UserId == user.Id &&
                    (postId.HasValue ? l.PostId == postId : l.CommentId == commentId));

            if (existingLike != null)
            {
                _context.ForumLikes.Remove(existingLike);
            }
            else
            {
                var like = new ForumLike
                {
                    UserId = user.Id,
                    PostId = postId,
                    CommentId = commentId
                };
                _context.ForumLikes.Add(like);
            }

            await _context.SaveChangesAsync();

            return Json(new { success = true });
        }
    }
}
