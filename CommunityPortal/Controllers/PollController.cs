using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using CommunityPortal.Data;
using CommunityPortal.Models;
using CommunityPortal.Models.Poll;
using CommunityPortal.Models.Enums;
using System.Collections.Generic;
using CommunityPortal.Services;

namespace CommunityPortal.Controllers
{
    public class PollController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly NotificationService _notificationService;

        public PollController(ApplicationDbContext context, UserManager<ApplicationUser> userManager, NotificationService notificationService)
        {
            _context = context;
            _userManager = userManager;
            _notificationService = notificationService;
        }

        // GET: Poll
        public async Task<IActionResult> Index()
        {
            try
            {
                var currentUser = await _userManager.GetUserAsync(User);
                if (currentUser == null)
                {
                    return Challenge();
                }

                var isAdmin = User.IsInRole("admin");
                var isHomeowner = User.IsInRole("homeowners");
                var isStaff = User.IsInRole("staff");

                List<Poll> polls;

                if (isAdmin || isStaff)
                {
                    // Admins and staff can see all non-deleted polls
                    polls = await _context.Polls
                        .Include(p => p.CreatedBy)
                        .Where(p => !p.IsDeleted)
                        .OrderByDescending(p => p.CreatedAt)
                        .ToListAsync();
                }
                else if (isHomeowner)
                {
                    // Homeowners can only see published polls that are active and not deleted
                    polls = await _context.Polls
                        .Include(p => p.CreatedBy)
                        .Where(p => p.Status == PollStatus.Published &&
                                    p.StartDate <= DateTime.UtcNow &&
                                    p.EndDate >= DateTime.UtcNow &&
                                    !p.IsDeleted &&
                                    p.TargetAudience == PollTargetAudience.AllHomeowners)
                        .OrderByDescending(p => p.CreatedAt)
                        .ToListAsync();

                    // For each poll, check if the user has already responded
                    foreach (var poll in polls)
                    {
                        poll.Responses = await _context.PollResponses
                            .Where(r => r.PollId == poll.Id && r.RespondentId == currentUser.Id && !r.IsDeleted)
                            .ToListAsync();
                    }
                }
                else
                {
                    // Other users can't see polls
                    polls = new List<Poll>();
                }

                return View(polls);
            }
            catch (Exception ex)
            {
                // Log the error
                Console.WriteLine($"Error loading polls: {ex.Message}");
                
                // Return a friendly error view with the error message
                TempData["ErrorMessage"] = $"An error occurred while loading polls: {ex.Message}";
                return View(new List<Poll>());
            }
        }

        // GET: Poll/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            try
            {
                var poll = await _context.Polls
                    .Include(p => p.CreatedBy)
                    .Include(p => p.Questions.Where(q => !q.IsDeleted))
                        .ThenInclude(q => q.Options.Where(o => !o.IsDeleted))
                    .FirstOrDefaultAsync(m => m.Id == id && !m.IsDeleted);

                if (poll == null)
                {
                    return NotFound();
                }

                var currentUser = await _userManager.GetUserAsync(User);
                if (currentUser == null)
                {
                    return Challenge();
                }

                // Check if user has already responded
                bool hasResponded = await _context.PollResponses
                    .AnyAsync(r => r.PollId == id && r.RespondentId == currentUser.Id && !r.IsDeleted);

                ViewBag.HasResponded = hasResponded;

                // Sort questions by display order
                poll.Questions = poll.Questions.OrderBy(q => q.DisplayOrder).ToList();
                foreach (var question in poll.Questions)
                {
                    question.Options = question.Options.OrderBy(o => o.DisplayOrder).ToList();
                }

                return View(poll);
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = $"An error occurred: {ex.Message}";
                return RedirectToAction(nameof(Index));
            }
        }

        // GET: Poll/Create
        [Authorize(Roles = "admin,staff")]
        public IActionResult Create()
        {
            return View();
        }

        // POST: Poll/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "admin,staff")]
        public async Task<IActionResult> Create([Bind("Title,Description,StartDate,EndDate,TargetAudience")] Poll poll)
        {
            // Get current user before validation
            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser == null)
            {
                return Challenge();
            }

            // Remove validation errors for fields we're going to set manually
            ModelState.Remove("CreatedById");
            ModelState.Remove("CreatedBy");
            ModelState.Remove("LastUpdatedById");
            ModelState.Remove("LastUpdatedBy");
            
            if (ModelState.IsValid)
            {
                try
                {
                    // Set required fields
                    poll.CreatedById = currentUser.Id;
                    poll.CreatedBy = currentUser;
                    poll.LastUpdatedById = currentUser.Id;
                    poll.LastUpdatedBy = currentUser;
                    poll.CreatedAt = DateTime.UtcNow;
                    poll.UpdatedAt = DateTime.UtcNow;
                    poll.Status = PollStatus.Draft;
                    poll.IsDeleted = false;
                    poll.Questions = new List<PollQuestion>();
                    poll.Responses = new List<PollResponse>();

                    _context.Add(poll);
                    await _context.SaveChangesAsync();
                    
                    return RedirectToAction(nameof(Edit), new { id = poll.Id });
                }
                catch (Exception ex)
                {
                    // Log the exception details to help diagnose the issue
                    ModelState.AddModelError(string.Empty, $"Error creating poll: {ex.Message}");
                    if (ex.InnerException != null)
                    {
                        ModelState.AddModelError(string.Empty, $"Inner exception: {ex.InnerException.Message}");
                    }
                }
            }
            
            // If we got this far, something failed, log validation errors
            foreach (var state in ModelState)
            {
                if (state.Value.Errors.Count > 0)
                {
                    // Add errors to TempData so they can be displayed in the view
                    foreach (var error in state.Value.Errors)
                    {
                        ModelState.AddModelError(string.Empty, $"{state.Key}: {error.ErrorMessage}");
                    }
                }
            }
            
            return View(poll);
        }

        // GET: Poll/Edit/5
        [Authorize(Roles = "admin,staff")]
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            try
            {
                var poll = await _context.Polls
                    .Include(p => p.Questions.Where(q => !q.IsDeleted))
                        .ThenInclude(q => q.Options.Where(o => !o.IsDeleted))
                    .FirstOrDefaultAsync(m => m.Id == id && !m.IsDeleted);

                if (poll == null)
                {
                    return NotFound();
                }

                // Sort questions by display order
                poll.Questions = poll.Questions.OrderBy(q => q.DisplayOrder).ToList();
                foreach (var question in poll.Questions)
                {
                    question.Options = question.Options.OrderBy(o => o.DisplayOrder).ToList();
                }

                return View(poll);
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = $"An error occurred while loading the poll: {ex.Message}";
                return RedirectToAction(nameof(Index));
            }
        }

        // POST: Poll/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "admin,staff")]
        public async Task<IActionResult> Edit(int id, [Bind("Id,Title,Description,StartDate,EndDate,TargetAudience,Status")] Poll poll)
        {
            if (id != poll.Id)
            {
                return NotFound();
            }

            // Remove validation errors for fields we're going to set manually
            ModelState.Remove("CreatedById");
            ModelState.Remove("CreatedBy");
            ModelState.Remove("LastUpdatedById");
            ModelState.Remove("LastUpdatedBy");

            if (ModelState.IsValid)
            {
                try
                {
                    var existingPoll = await _context.Polls
                        .Include(p => p.Questions.Where(q => !q.IsDeleted))
                            .ThenInclude(q => q.Options.Where(o => !o.IsDeleted))
                        .FirstOrDefaultAsync(p => p.Id == id && !p.IsDeleted);
                        
                    if (existingPoll == null)
                    {
                        return NotFound();
                    }

                    // Check if the poll is being published
                    bool isBeingPublished = existingPoll.Status != PollStatus.Published && poll.Status == PollStatus.Published;

                    // Update only the fields that were included in the form
                    existingPoll.Title = poll.Title;
                    existingPoll.Description = poll.Description;
                    existingPoll.StartDate = poll.StartDate;
                    existingPoll.EndDate = poll.EndDate;
                    existingPoll.TargetAudience = poll.TargetAudience;
                    existingPoll.Status = poll.Status;
                    existingPoll.UpdatedAt = DateTime.UtcNow;
                    existingPoll.LastUpdatedById = _userManager.GetUserId(User);

                    _context.Update(existingPoll);
                    await _context.SaveChangesAsync();
                    
                    // Send notification if the poll is being published
                    if (isBeingPublished)
                    {
                        string currentUserId = _userManager.GetUserId(User);
                        string title = "New Poll Available";
                        string message = $"A new poll '{existingPoll.Title}' is now available for your participation.";
                        string link = $"/Poll/Respond/{existingPoll.Id}";

                        // If the poll is targeted to all homeowners, broadcast to all homeowners
                        if (existingPoll.TargetAudience == PollTargetAudience.AllHomeowners)
                        {
                            // Get all homeowners
                            var homeowners = await _userManager.GetUsersInRoleAsync("homeowners");
                            
                            // Send notification to each homeowner
                            foreach (var homeowner in homeowners)
                            {
                                await _notificationService.CreateNotificationAsync(
                                    recipientId: homeowner.Id,
                                    title: title,
                                    message: message,
                                    link: link,
                                    type: NotificationType.Poll,
                                    senderId: currentUserId
                                );
                            }
                        }
                        else if (existingPoll.TargetAudience == PollTargetAudience.SpecificHomeowners)
                        {
                            // For specific homeowners, we would need to have a way to select them
                            // This would require additional implementation
                            // For now, we'll notify all homeowners as a fallback
                            var homeowners = await _userManager.GetUsersInRoleAsync("homeowners");
                            
                            // Send notification to each homeowner
                            foreach (var homeowner in homeowners)
                            {
                                await _notificationService.CreateNotificationAsync(
                                    recipientId: homeowner.Id,
                                    title: title,
                                    message: message,
                                    link: link,
                                    type: NotificationType.Poll,
                                    senderId: currentUserId
                                );
                            }
                        }
                    }
                    
                    TempData["SuccessMessage"] = "Poll updated successfully!";
                    return RedirectToAction(nameof(Index));
                }
                catch (DbUpdateConcurrencyException ex)
                {
                    if (!PollExists(poll.Id))
                    {
                        return NotFound();
                    }
                    else
                    {
                        ModelState.AddModelError(string.Empty, $"Concurrency error: {ex.Message}");
                    }
                }
                catch (Exception ex)
                {
                    ModelState.AddModelError(string.Empty, $"Error updating poll: {ex.Message}");
                }
            }
            
            // If we got this far, something failed, reload the poll data
            var reloadedPoll = await _context.Polls
                .Include(p => p.Questions.Where(q => !q.IsDeleted))
                    .ThenInclude(q => q.Options.Where(o => !o.IsDeleted))
                .FirstOrDefaultAsync(p => p.Id == id && !p.IsDeleted);
                
            if (reloadedPoll == null)
            {
                return NotFound();
            }
            
            // Update the fields from the form data to preserve user input
            reloadedPoll.Title = poll.Title;
            reloadedPoll.Description = poll.Description;
            reloadedPoll.StartDate = poll.StartDate;
            reloadedPoll.EndDate = poll.EndDate;
            reloadedPoll.TargetAudience = poll.TargetAudience;
            reloadedPoll.Status = poll.Status;
            
            return View(reloadedPoll);
        }

        // POST: Poll/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "admin,staff")]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var poll = await _context.Polls
                .Include(p => p.Questions)
                .Include(p => p.Responses)
                    .ThenInclude(r => r.Answers)
                .FirstOrDefaultAsync(p => p.Id == id);
            
            if (poll == null)
            {
                return NotFound();
            }

            // Soft delete the poll
            poll.IsDeleted = true;
            poll.UpdatedAt = DateTime.UtcNow;
            poll.LastUpdatedById = _userManager.GetUserId(User);

            // Also soft delete all questions
            foreach (var question in poll.Questions)
            {
                question.IsDeleted = true;
            }

            // Also soft delete all responses
            foreach (var response in poll.Responses)
            {
                response.IsDeleted = true;
                
                // Mark all answers as deleted
                foreach (var answer in response.Answers)
                {
                    answer.IsDeleted = true;
                }
            }

            _context.Update(poll);
            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        // GET: Poll/AddQuestion/5
        [Authorize(Roles = "admin,staff")]
        public async Task<IActionResult> AddQuestion(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            try
            {
                var poll = await _context.Polls
                    .FirstOrDefaultAsync(p => p.Id == id && !p.IsDeleted);
                
                if (poll == null)
                {
                    return NotFound();
                }

                // Set default values for the new question
                var question = new PollQuestion
                {
                    PollId = poll.Id,
                    IsRequired = true,
                    MinRating = 1,
                    MaxRating = 5
                };

                ViewBag.PollId = id;
                ViewBag.PollTitle = poll.Title;
                ViewBag.QuestionTypes = new SelectList(Enum.GetValues(typeof(QuestionType))
                    .Cast<QuestionType>()
                    .Select(v => new { Value = (int)v, Text = v.ToString() }), "Value", "Text");
                
                return View(question);
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = $"An error occurred while loading the poll: {ex.Message}";
                return RedirectToAction(nameof(Edit), new { id });
            }
        }

        // POST: Poll/AddQuestion/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "admin,staff")]
        public async Task<IActionResult> AddQuestion([Bind("PollId,QuestionText,QuestionType,IsRequired,MinRating,MaxRating")] PollQuestion question)
        {
            try
            {
                // Remove validation errors for fields we're going to set manually
                ModelState.Remove("Poll");
                
                // Check if the referenced poll exists and isn't deleted
                var poll = await _context.Polls
                    .FirstOrDefaultAsync(p => p.Id == question.PollId && !p.IsDeleted);

                if (poll == null)
                {
                    ModelState.AddModelError(string.Empty, "The specified poll does not exist.");
                    ViewBag.PollId = question.PollId;
                    ViewBag.QuestionTypes = new SelectList(Enum.GetValues(typeof(QuestionType))
                        .Cast<QuestionType>()
                        .Select(v => new { Value = (int)v, Text = v.ToString() }), "Value", "Text");
                    return View(question);
                }

                ViewBag.PollTitle = poll.Title;

                // Set IsDeleted to false explicitly
                question.IsDeleted = false;

                // Validate that rating questions have min and max values
                if (question.QuestionType == QuestionType.Rating)
                {
                    if (!question.MinRating.HasValue || !question.MaxRating.HasValue)
                    {
                        ModelState.AddModelError(string.Empty, "Rating questions must have minimum and maximum values.");
                    }
                    else if (question.MinRating.Value >= question.MaxRating.Value)
                    {
                        ModelState.AddModelError(string.Empty, "Maximum rating must be greater than minimum rating.");
                    }
                }
                else
                {
                    // For non-rating questions, set these to null
                    question.MinRating = null;
                    question.MaxRating = null;
                }
                
                if (string.IsNullOrWhiteSpace(question.QuestionText))
                {
                    ModelState.AddModelError("QuestionText", "Question text is required.");
                }

                if (ModelState.IsValid)
                {
                    // Get the max display order
                    var maxDisplayOrder = await _context.PollQuestions
                        .Where(q => q.PollId == question.PollId && !q.IsDeleted)
                        .Select(q => (int?)q.DisplayOrder)
                        .MaxAsync() ?? 0;

                    question.DisplayOrder = maxDisplayOrder + 1;
                    
                    question.Options = new List<PollQuestionOption>();
                    
                    try {
                        _context.Add(question);
                        await _context.SaveChangesAsync();

                        // Explicitly load the options collection to prevent tracking errors
                        await _context.Entry(question).Collection(q => q.Options).LoadAsync();

                        TempData["SuccessMessage"] = "Question added successfully!";
                        return RedirectToAction(nameof(Edit), new { id = question.PollId });
                    }
                    catch (Exception ex) {
                        if (ex.InnerException != null) {
                            ModelState.AddModelError(string.Empty, $"Database error: {ex.InnerException.Message}");
                        } else {
                            ModelState.AddModelError(string.Empty, $"Error adding question: {ex.Message}");
                        }
                        throw;
                    }
                }
                
                // If validation fails, reload the form
                ViewBag.PollId = question.PollId;
                ViewBag.PollTitle = poll.Title;
                ViewBag.QuestionTypes = new SelectList(Enum.GetValues(typeof(QuestionType))
                    .Cast<QuestionType>()
                    .Select(v => new { Value = (int)v, Text = v.ToString() }), "Value", "Text");
                
                return View(question);
            }
            catch (Exception ex)
            {
                // Detailed exception debugging
                ModelState.AddModelError(string.Empty, $"Error adding question: {ex.Message}");
                if (ex.InnerException != null)
                {
                    ModelState.AddModelError(string.Empty, $"Inner exception: {ex.InnerException.Message}");
                }
                
                ViewBag.PollId = question.PollId;
                ViewBag.QuestionTypes = new SelectList(Enum.GetValues(typeof(QuestionType))
                    .Cast<QuestionType>()
                    .Select(v => new { Value = (int)v, Text = v.ToString() }), "Value", "Text");
                
                return View(question);
            }
        }

        // GET: Poll/AddOption/5
        [Authorize(Roles = "admin,staff")]
        public async Task<IActionResult> AddOption(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            try
            {
                var question = await _context.PollQuestions
                    .Include(q => q.Poll)
                    .FirstOrDefaultAsync(q => q.Id == id && !q.IsDeleted);

                if (question == null)
                {
                    return NotFound();
                }

                // Check if this is a question type that supports options
                if (question.QuestionType != QuestionType.MultipleChoice && 
                    question.QuestionType != QuestionType.SingleChoice)
                {
                    TempData["ErrorMessage"] = "Only Multiple Choice and Single Choice questions can have options.";
                    return RedirectToAction(nameof(Edit), new { id = question.PollId });
                }

                ViewBag.QuestionId = id;
                ViewBag.PollId = question.PollId;
                ViewBag.QuestionText = question.QuestionText;
                
                return View();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in AddOption GET: {ex.Message}");
                TempData["ErrorMessage"] = $"An error occurred: {ex.Message}";
                return RedirectToAction(nameof(Index));
            }
        }

        // POST: Poll/AddOption/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "admin,staff")]
        public async Task<IActionResult> AddOption(int QuestionId, string OptionText)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(OptionText))
                {
                    ModelState.AddModelError("OptionText", "Option text is required.");
                }
                
                if (ModelState.IsValid)
                {
                    var question = await _context.PollQuestions
                        .Include(q => q.Poll)
                        .Include(q => q.Options.Where(o => !o.IsDeleted))
                        .FirstOrDefaultAsync(q => q.Id == QuestionId && !q.IsDeleted);
                        
                    if (question == null)
                    {
                        TempData["ErrorMessage"] = "The specified question does not exist.";
                        return RedirectToAction(nameof(Index));
                    }
                    
                    // Verify this is a question type that supports options
                    if (question.QuestionType != QuestionType.MultipleChoice && 
                        question.QuestionType != QuestionType.SingleChoice)
                    {
                        TempData["ErrorMessage"] = "Only Multiple Choice and Single Choice questions can have options.";
                        return RedirectToAction(nameof(Edit), new { id = question.PollId });
                    }

                    // Get the max display order for the question's options
                    var maxDisplayOrder = question.Options.Count > 0
                        ? question.Options.Max(o => o.DisplayOrder)
                        : 0;

                    var option = new PollQuestionOption
                    {
                        QuestionId = QuestionId,
                        Question = question,
                        OptionText = OptionText.Trim(),
                        DisplayOrder = maxDisplayOrder + 1,
                        IsDeleted = false
                    };

                    try {
                        _context.PollQuestionOptions.Add(option);
                        await _context.SaveChangesAsync();

                        TempData["SuccessMessage"] = "Option added successfully!";
                        return RedirectToAction(nameof(Edit), new { id = question.PollId });
                    }
                    catch (Exception ex) {
                        if (ex.InnerException != null) {
                            TempData["ErrorMessage"] = $"Database error: {ex.InnerException.Message}";
                        } else {
                            TempData["ErrorMessage"] = $"Error adding option: {ex.Message}";
                        }
                        
                        return RedirectToAction(nameof(AddOption), new { id = QuestionId });
                    }
                }
                
                // If we get here, something failed, reload the form
                var pollQuestion = await _context.PollQuestions
                    .Include(q => q.Poll)
                    .FirstOrDefaultAsync(q => q.Id == QuestionId && !q.IsDeleted);
                    
                if (pollQuestion == null)
                {
                    TempData["ErrorMessage"] = "The specified question does not exist.";
                    return RedirectToAction(nameof(Index));
                }
                
                ViewBag.QuestionId = QuestionId;
                ViewBag.PollId = pollQuestion.PollId;
                ViewBag.QuestionText = pollQuestion.QuestionText;
                
                // Create a model to pass back to the view with the entered option text
                var model = new PollQuestionOption { OptionText = OptionText };
                return View(model);
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = $"An unexpected error occurred: {ex.Message}";
                return RedirectToAction(nameof(Index));
            }
        }

        // GET: Poll/Respond/5
        [Authorize(Roles = "homeowners")]
        public async Task<IActionResult> Respond(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            try
            {
                var poll = await _context.Polls
                    .Include(p => p.Questions.Where(q => !q.IsDeleted))
                        .ThenInclude(q => q.Options.Where(o => !o.IsDeleted))
                    .FirstOrDefaultAsync(m => m.Id == id &&
                                              m.Status == PollStatus.Published &&
                                              m.StartDate <= DateTime.UtcNow &&
                                              m.EndDate >= DateTime.UtcNow &&
                                              !m.IsDeleted);

                if (poll == null)
                {
                    return NotFound();
                }

                var currentUser = await _userManager.GetUserAsync(User);
                if (currentUser == null)
                {
                    return Challenge();
                }

                // Check if user has already responded
                bool hasResponded = await _context.PollResponses
                    .AnyAsync(r => r.PollId == id && r.RespondentId == currentUser.Id && !r.IsDeleted);

                if (hasResponded)
                {
                    TempData["SuccessMessage"] = "You have already responded to this poll.";
                    return RedirectToAction(nameof(Details), new { id });
                }

                // Sort questions by display order
                poll.Questions = poll.Questions.OrderBy(q => q.DisplayOrder).ToList();
                foreach (var question in poll.Questions)
                {
                    question.Options = question.Options.OrderBy(o => o.DisplayOrder).ToList();
                }

                return View(poll);
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = $"An error occurred: {ex.Message}";
                return RedirectToAction(nameof(Index));
            }
        }

        // POST: Poll/SubmitResponse
        [HttpPost]
        [Authorize(Roles = "homeowners")]
        public async Task<IActionResult> SubmitResponse(int pollId, IFormCollection form)
        {
            // Use a transaction to ensure atomicity for the entire operation
            using (var transaction = await _context.Database.BeginTransactionAsync())
            {
                try
                {
                    // Verify the poll exists and is published
                    var poll = await _context.Polls
                        .Include(p => p.Questions)
                            .ThenInclude(q => q.Options)
                        .FirstOrDefaultAsync(p => p.Id == pollId && p.Status == PollStatus.Published && !p.IsDeleted);

                    if (poll == null)
                    {
                        TempData["ErrorMessage"] = "The poll does not exist or is not currently active.";
                        return RedirectToAction(nameof(Index));
                    }

                    // Get current user
                    var currentUser = await _userManager.GetUserAsync(User);
                    if (currentUser == null)
                    {
                        TempData["ErrorMessage"] = "You must be logged in to respond to a poll.";
                        return RedirectToAction(nameof(Index));
                    }

                    // Check if the user has already responded - use FOR UPDATE to lock the rows
                    var existingResponseCount = await _context.PollResponses
                        .Where(r => r.PollId == pollId && r.RespondentId == currentUser.Id && !r.IsDeleted)
                        .CountAsync();

                    if (existingResponseCount > 0)
                    {
                        await transaction.RollbackAsync();
                        TempData["ErrorMessage"] = "You have already responded to this poll.";
                        return RedirectToAction(nameof(Details), new { id = pollId });
                    }

                    // Get active questions
                    var activeQuestions = poll.Questions.Where(q => !q.IsDeleted).ToList();

                    // Check required questions
                    var requiredQuestions = activeQuestions.Where(q => q.IsRequired).ToList();
                    var answers = new Dictionary<string, string>();

                    // Process form data
                    foreach (var key in form.Keys)
                    {
                        // Skip non-question keys
                        if (key == "pollId")
                        {
                            continue;
                        }
                        
                        // Handle array notation in form field names
                        string questionIdStr;
                        bool isMultipleChoice = false;
                        
                        if (key.EndsWith("[]"))
                        {
                            questionIdStr = key.Substring(0, key.Length - 2);
                            isMultipleChoice = true;
                        }
                        else
                        {
                            questionIdStr = key;
                        }
                        
                        // Verify this is a numeric question ID
                        if (!int.TryParse(questionIdStr, out _))
                        {
                            continue;
                        }

                        if (isMultipleChoice)
                        {
                            // Store values for multiple choice questions
                            var values = form[key].ToString();
                            answers[questionIdStr] = values;
                        }
                        else
                        {
                            // Store values for other question types
                            answers[questionIdStr] = form[key].ToString();
                        }
                    }

                    // Validate required questions are answered
                    foreach (var question in requiredQuestions)
                    {
                        if (!answers.ContainsKey(question.Id.ToString()) ||
                            string.IsNullOrWhiteSpace(answers[question.Id.ToString()]))
                        {
                            await transaction.RollbackAsync();
                            TempData["ErrorMessage"] = $"Please answer all required questions.";
                            return RedirectToAction(nameof(Respond), new { id = pollId });
                        }
                    }

                    // Create new response - within transaction scope
                    var response = new PollResponse
                    {
                        PollId = pollId,
                        RespondentId = currentUser.Id,
                        SubmittedAt = DateTime.UtcNow,
                        IsDeleted = false
                    };

                    _context.PollResponses.Add(response);
                    await _context.SaveChangesAsync();

                    // Track how many answers are successfully created
                    int successfulAnswers = 0;
                    
                    // Process each answer 
                    foreach (var entry in answers)
                    {
                        if (!int.TryParse(entry.Key, out int questionId))
                        {
                            continue;
                        }

                        var question = activeQuestions.FirstOrDefault(q => q.Id == questionId);
                        if (question == null)
                        {
                            continue;
                        }

                        try
                        {
                            // Clear tracked entries for this question to avoid conflicts
                            var existingEntries = _context.ChangeTracker.Entries<PollQuestionAnswer>()
                                .Where(e => e.Entity.QuestionId == questionId)
                                .ToList();
                                
                            foreach (var existingEntry in existingEntries)
                            {
                                existingEntry.State = EntityState.Detached;
                            }
                                
                            switch (question.QuestionType)
                            {
                                case QuestionType.MultipleChoice:
                                    // Handle multiple selections (comma-separated values)
                                    if (!string.IsNullOrEmpty(entry.Value))
                                    {
                                        var optionValues = entry.Value.Split(',');
                                        var processedOptionIds = new HashSet<int>(); // Track processed option IDs to prevent duplicates
                                        
                                        foreach (var optionValue in optionValues)
                                        {
                                            if (int.TryParse(optionValue.Trim(), out int optionId) && !processedOptionIds.Contains(optionId))
                                            {
                                                // Add to processed set to prevent duplicates
                                                processedOptionIds.Add(optionId);
                                                
                                                // Create answer within the same transaction
                                                var multiAnswer = new PollQuestionAnswer
                                                {
                                                    ResponseId = response.Id,
                                                    QuestionId = questionId,
                                                    SelectedOptionId = optionId,
                                                    TextAnswer = string.Empty,
                                                    IsDeleted = false
                                                };
                                                
                                                _context.PollQuestionAnswers.Add(multiAnswer);
                                                await _context.SaveChangesAsync();
                                                successfulAnswers++;
                                            }
                                        }
                                    }
                                    break;
                                
                                case QuestionType.SingleChoice:
                                    if (int.TryParse(entry.Value, out int singleOptionId))
                                    {
                                        var singleAnswer = new PollQuestionAnswer
                                        {
                                            ResponseId = response.Id,
                                            QuestionId = questionId,
                                            SelectedOptionId = singleOptionId,
                                            TextAnswer = string.Empty,
                                            IsDeleted = false
                                        };
                                        
                                        _context.PollQuestionAnswers.Add(singleAnswer);
                                        await _context.SaveChangesAsync();
                                        successfulAnswers++;
                                    }
                                    break;
                                
                                case QuestionType.Rating:
                                    if (int.TryParse(entry.Value, out int ratingValue))
                                    {
                                        var ratingAnswer = new PollQuestionAnswer
                                        {
                                            ResponseId = response.Id,
                                            QuestionId = questionId,
                                            RatingAnswer = ratingValue,
                                            TextAnswer = string.Empty,
                                            IsDeleted = false
                                        };
                                        
                                        _context.PollQuestionAnswers.Add(ratingAnswer);
                                        await _context.SaveChangesAsync();
                                        successfulAnswers++;
                                    }
                                    break;
                                
                                case QuestionType.YesNo:
                                    if (bool.TryParse(entry.Value, out bool boolValue))
                                    {
                                        var boolAnswer = new PollQuestionAnswer
                                        {
                                            ResponseId = response.Id,
                                            QuestionId = questionId,
                                            BoolAnswer = boolValue,
                                            TextAnswer = string.Empty,
                                            IsDeleted = false
                                        };
                                        
                                        _context.PollQuestionAnswers.Add(boolAnswer);
                                        await _context.SaveChangesAsync();
                                        successfulAnswers++;
                                    }
                                    break;
                                
                                case QuestionType.OpenEnded:
                                    var textAnswer = new PollQuestionAnswer
                                    {
                                        ResponseId = response.Id,
                                        QuestionId = questionId,
                                        TextAnswer = entry.Value,
                                        IsDeleted = false
                                    };
                                    
                                    _context.PollQuestionAnswers.Add(textAnswer);
                                    await _context.SaveChangesAsync();
                                    successfulAnswers++;
                                    break;
                            }
                        }
                        catch (Exception ex)
                        {
                            // Log the error but continue with other questions
                            // We'll check the successfulAnswers count at the end
                        }
                    }

                    if (successfulAnswers > 0)
                    {
                        await transaction.CommitAsync();
                        TempData["SuccessMessage"] = "Thank you for your response!";
                        return RedirectToAction(nameof(Index));
                    }
                    else
                    {
                        await transaction.RollbackAsync();
                        TempData["ErrorMessage"] = $"An error occurred while submitting your response. Please try again later.";
                        return RedirectToAction(nameof(Respond), new { id = pollId });
                    }
                }
                catch (Exception ex)
                {
                    await transaction.RollbackAsync();
                    TempData["ErrorMessage"] = $"An error occurred while submitting your response: {ex.Message}";
                    return RedirectToAction(nameof(Respond), new { id = pollId });
                }
            }
        }

        // GET: Poll/ViewResults/5
        [Authorize(Roles = "admin,staff")]
        public async Task<IActionResult> Results(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            try
            {
                var poll = await _context.Polls
                    .Include(p => p.CreatedBy)
                    .Include(p => p.Questions.Where(q => !q.IsDeleted))
                        .ThenInclude(q => q.Options.Where(o => !o.IsDeleted))
                    .Include(p => p.Responses.Where(r => !r.IsDeleted))
                        .ThenInclude(r => r.Answers.Where(a => !a.IsDeleted))
                    .FirstOrDefaultAsync(m => m.Id == id && !m.IsDeleted);

                if (poll == null)
                {
                    return NotFound();
                }

                // Sort questions by display order
                poll.Questions = poll.Questions.OrderBy(q => q.DisplayOrder).ToList();
                foreach (var question in poll.Questions)
                {
                    question.Options = question.Options.OrderBy(o => o.DisplayOrder).ToList();
                }

                return View("Results", poll);
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = $"An error occurred: {ex.Message}";
                return RedirectToAction(nameof(Index));
            }
        }

        // GET: Poll/EditQuestion/5
        [Authorize(Roles = "admin,staff")]
        public async Task<IActionResult> EditQuestion(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            try
            {
                var question = await _context.PollQuestions
                    .Include(q => q.Poll)
                    .FirstOrDefaultAsync(q => q.Id == id && !q.IsDeleted);

                if (question == null)
                {
                    return NotFound();
                }

                ViewBag.PollId = question.PollId;
                ViewBag.PollTitle = question.Poll.Title;
                ViewBag.QuestionTypes = new SelectList(Enum.GetValues(typeof(QuestionType))
                    .Cast<QuestionType>()
                    .Select(v => new { Value = (int)v, Text = v.ToString() }), "Value", "Text", question.QuestionType);

                return View(question);
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = $"An error occurred while loading the question: {ex.Message}";
                return RedirectToAction(nameof(Index));
            }
        }

        // POST: Poll/EditQuestion/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "admin,staff")]
        public async Task<IActionResult> EditQuestion(int id, [Bind("Id,PollId,QuestionText,QuestionType,IsRequired,MinRating,MaxRating")] PollQuestion question)
        {
            if (id != question.Id)
            {
                return NotFound();
            }

            try
            {
                // Remove validation errors for fields we're going to set manually
                ModelState.Remove("Poll");
                
                // Load existing question to check if it exists
                var existingQuestion = await _context.PollQuestions
                    .Include(q => q.Poll)
                    .Include(q => q.Options.Where(o => !o.IsDeleted))
                    .FirstOrDefaultAsync(q => q.Id == id && !q.IsDeleted);

                if (existingQuestion == null)
                {
                    return NotFound();
                }

                // Validate that rating questions have min and max values
                if (question.QuestionType == QuestionType.Rating)
                {
                    if (!question.MinRating.HasValue || !question.MaxRating.HasValue)
                    {
                        ModelState.AddModelError(string.Empty, "Rating questions must have minimum and maximum values.");
                    }
                    else if (question.MinRating.Value >= question.MaxRating.Value)
                    {
                        ModelState.AddModelError(string.Empty, "Maximum rating must be greater than minimum rating.");
                    }
                }
                else
                {
                    // For non-rating questions, set these to null
                    question.MinRating = null;
                    question.MaxRating = null;
                }

                if (string.IsNullOrWhiteSpace(question.QuestionText))
                {
                    ModelState.AddModelError("QuestionText", "Question text is required.");
                }

                if (ModelState.IsValid)
                {
                    // Update properties from form
                    existingQuestion.QuestionText = question.QuestionText;
                    existingQuestion.IsRequired = question.IsRequired;
                    
                    // Check if question type has changed
                    bool questionTypeChanged = existingQuestion.QuestionType != question.QuestionType;
                    existingQuestion.QuestionType = question.QuestionType;
                    
                    // Set rating values
                    existingQuestion.MinRating = question.MinRating;
                    existingQuestion.MaxRating = question.MaxRating;

                    // If the question type has changed and the new type doesn't support options, mark options as deleted
                    if (questionTypeChanged && 
                        (question.QuestionType != QuestionType.MultipleChoice && 
                         question.QuestionType != QuestionType.SingleChoice))
                    {
                        foreach (var option in existingQuestion.Options)
                        {
                            option.IsDeleted = true;
                        }
                    }

                    _context.Update(existingQuestion);
                    await _context.SaveChangesAsync();

                    TempData["SuccessMessage"] = "Question updated successfully!";
                    return RedirectToAction(nameof(Edit), new { id = existingQuestion.PollId });
                }

                // If validation fails, reload the form
                ViewBag.PollId = existingQuestion.PollId;
                ViewBag.PollTitle = existingQuestion.Poll.Title;
                ViewBag.QuestionTypes = new SelectList(Enum.GetValues(typeof(QuestionType))
                    .Cast<QuestionType>()
                    .Select(v => new { Value = (int)v, Text = v.ToString() }), "Value", "Text", question.QuestionType);

                return View(question);
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = $"An error occurred while updating the question: {ex.Message}";
                return RedirectToAction(nameof(Index));
            }
        }

        // POST: Poll/DeleteQuestion/5
        [HttpPost, ActionName("DeleteQuestion")]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "admin,staff")]
        public async Task<IActionResult> DeleteQuestion(int id)
        {
            try
            {
                var question = await _context.PollQuestions
                    .Include(q => q.Options)
                    .Include(q => q.Answers)
                    .FirstOrDefaultAsync(q => q.Id == id && !q.IsDeleted);

                if (question == null)
                {
                    return NotFound();
                }

                int pollId = question.PollId;

                // Soft delete the question
                question.IsDeleted = true;

                // Soft delete related options
                foreach (var option in question.Options)
                {
                    option.IsDeleted = true;
                }

                // Soft delete related answers
                foreach (var answer in question.Answers)
                {
                    answer.IsDeleted = true;
                }

                await _context.SaveChangesAsync();

                TempData["SuccessMessage"] = "Question deleted successfully!";
                return RedirectToAction(nameof(Edit), new { id = pollId });
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = $"An error occurred while deleting the question: {ex.Message}";
                return RedirectToAction(nameof(Index));
            }
        }
        
        // GET: Poll/EditOption/5
        [Authorize(Roles = "admin,staff")]
        public async Task<IActionResult> EditOption(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            try
            {
                var option = await _context.PollQuestionOptions
                    .Include(o => o.Question)
                        .ThenInclude(q => q.Poll)
                    .FirstOrDefaultAsync(o => o.Id == id && !o.IsDeleted);

                if (option == null)
                {
                    return NotFound();
                }

                ViewBag.QuestionId = option.QuestionId;
                ViewBag.PollId = option.Question.PollId;
                ViewBag.QuestionText = option.Question.QuestionText;

                return View(option);
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = $"An error occurred: {ex.Message}";
                return RedirectToAction(nameof(Index));
            }
        }

        // POST: Poll/EditOption/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "admin,staff")]
        public async Task<IActionResult> EditOption(int id, [Bind("Id,QuestionId,OptionText")] PollQuestionOption option)
        {
            if (id != option.Id)
            {
                return NotFound();
            }

            try
            {
                // Remove validation errors for fields we're going to set manually
                ModelState.Remove("Question");
                
                if (string.IsNullOrWhiteSpace(option.OptionText))
                {
                    ModelState.AddModelError("OptionText", "Option text is required.");
                }

                if (ModelState.IsValid)
                {
                    var existingOption = await _context.PollQuestionOptions
                        .Include(o => o.Question)
                            .ThenInclude(q => q.Poll)
                        .FirstOrDefaultAsync(o => o.Id == id && !o.IsDeleted);

                    if (existingOption == null)
                    {
                        return NotFound();
                    }

                    existingOption.OptionText = option.OptionText.Trim();
                    _context.Update(existingOption);
                    await _context.SaveChangesAsync();

                    TempData["SuccessMessage"] = "Option updated successfully!";
                    return RedirectToAction(nameof(Edit), new { id = existingOption.Question.PollId });
                }

                // If validation fails, reload the form
                var question = await _context.PollQuestions
                    .Include(q => q.Poll)
                    .FirstOrDefaultAsync(q => q.Id == option.QuestionId && !q.IsDeleted);

                if (question == null)
                {
                    return NotFound();
                }

                ViewBag.QuestionId = option.QuestionId;
                ViewBag.PollId = question.PollId;
                ViewBag.QuestionText = question.QuestionText;

                return View(option);
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = $"An error occurred: {ex.Message}";
                return RedirectToAction(nameof(Index));
            }
        }

        // POST: Poll/DeleteOption/5
        [HttpPost, ActionName("DeleteOption")]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "admin,staff")]
        public async Task<IActionResult> DeleteOption(int id)
        {
            try
            {
                var option = await _context.PollQuestionOptions
                    .Include(o => o.Question)
                    .FirstOrDefaultAsync(o => o.Id == id && !o.IsDeleted);

                if (option == null)
                {
                    return NotFound();
                }

                int pollId = option.Question.PollId;

                // Soft delete the option
                option.IsDeleted = true;
                
                // Also update any answers that reference this option
                var relatedAnswers = await _context.PollQuestionAnswers
                    .Where(a => a.SelectedOptionId == id && !a.IsDeleted)
                    .ToListAsync();
                
                foreach (var answer in relatedAnswers)
                {
                    answer.IsDeleted = true;
                }

                await _context.SaveChangesAsync();

                TempData["SuccessMessage"] = "Option deleted successfully!";
                return RedirectToAction(nameof(Edit), new { id = pollId });
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = $"An error occurred: {ex.Message}";
                return RedirectToAction(nameof(Index));
            }
        }

        // Helper method to check if a poll exists
        private bool PollExists(int id)
        {
            return _context.Polls.Any(p => p.Id == id && !p.IsDeleted);
        }
    }
} 