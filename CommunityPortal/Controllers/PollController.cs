using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using CommunityPortal.Data;
using CommunityPortal.Models;
using CommunityPortal.Models.Poll;
using CommunityPortal.Models.Enums;

namespace CommunityPortal.Controllers
{
    public class PollController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public PollController(ApplicationDbContext context, UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
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
                var isBoardMember = User.IsInRole("BoardMember");

                List<Poll> polls;

                if (isAdmin)
                {
                    // Admins can see all non-deleted polls
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
                                    (p.TargetAudience == PollTargetAudience.AllHomeowners ||
                                     (p.TargetAudience == PollTargetAudience.BoardMembers && isBoardMember)))
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
                TempData["Error"] = $"An error occurred while loading polls: {ex.Message}";
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
                TempData["Error"] = $"An error occurred: {ex.Message}";
                return RedirectToAction(nameof(Index));
            }
        }

        // GET: Poll/Create
        [Authorize(Roles = "admin")]
        public IActionResult Create()
        {
            return View();
        }

        // POST: Poll/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "admin")]
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
        [Authorize(Roles = "admin")]
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
                TempData["Error"] = $"An error occurred while loading the poll: {ex.Message}";
                return RedirectToAction(nameof(Index));
            }
        }

        // POST: Poll/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "admin")]
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
                    
                    TempData["Success"] = "Poll updated successfully!";
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
        [Authorize(Roles = "admin")]
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
        [Authorize(Roles = "admin")]
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
                TempData["Error"] = $"An error occurred while loading the poll: {ex.Message}";
                return RedirectToAction(nameof(Edit), new { id });
            }
        }

        // POST: Poll/AddQuestion/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "admin")]
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

                        TempData["Success"] = "Question added successfully!";
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
        [Authorize(Roles = "admin")]
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
                    TempData["Error"] = "Only Multiple Choice and Single Choice questions can have options.";
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
                TempData["Error"] = $"An error occurred: {ex.Message}";
                return RedirectToAction(nameof(Index));
            }
        }

        // POST: Poll/AddOption/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "admin")]
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
                        TempData["Error"] = "The specified question does not exist.";
                        return RedirectToAction(nameof(Index));
                    }
                    
                    // Verify this is a question type that supports options
                    if (question.QuestionType != QuestionType.MultipleChoice && 
                        question.QuestionType != QuestionType.SingleChoice)
                    {
                        TempData["Error"] = "Only Multiple Choice and Single Choice questions can have options.";
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

                        TempData["Success"] = "Option added successfully!";
                        return RedirectToAction(nameof(Edit), new { id = question.PollId });
                    }
                    catch (Exception ex) {
                        if (ex.InnerException != null) {
                            TempData["Error"] = $"Database error: {ex.InnerException.Message}";
                        } else {
                            TempData["Error"] = $"Error adding option: {ex.Message}";
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
                    TempData["Error"] = "The specified question does not exist.";
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
                TempData["Error"] = $"An unexpected error occurred: {ex.Message}";
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
                    TempData["Message"] = "You have already responded to this poll.";
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
                TempData["Error"] = $"An error occurred: {ex.Message}";
                return RedirectToAction(nameof(Index));
            }
        }

        // POST: Poll/SubmitResponse
        [HttpPost]
        [Authorize(Roles = "admin, homeowners")]
        public async Task<IActionResult> SubmitResponse(int pollId, IFormCollection form)
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
                    TempData["Error"] = "The poll does not exist or is not currently active.";
                    return RedirectToAction(nameof(Index));
                }

                // Get current user
                var currentUser = await _userManager.GetUserAsync(User);
                if (currentUser == null)
                {
                    TempData["Error"] = "You must be logged in to respond to a poll.";
                    return RedirectToAction(nameof(Index));
                }

                // Check if the user has already responded
                var existingResponse = await _context.PollResponses
                    .AnyAsync(r => r.PollId == pollId && r.RespondentId == currentUser.Id && !r.IsDeleted);

                if (existingResponse)
                {
                    TempData["Error"] = "You have already responded to this poll.";
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
                        TempData["Error"] = $"Please answer all required questions.";
                        return RedirectToAction(nameof(Respond), new { id = pollId });
                    }
                }

                // Create new response
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
                
                // Process each answer individually to avoid tracking issues
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
                                    
                                    foreach (var optionValue in optionValues)
                                    {
                                        if (int.TryParse(optionValue.Trim(), out int optionId))
                                        {
                                            // Create a new answer for each selection with a fresh context
                                            using (var transaction = await _context.Database.BeginTransactionAsync())
                                            {
                                                try
                                                {
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
                                                    await transaction.CommitAsync();
                                                    successfulAnswers++;
                                                }
                                                catch
                                                {
                                                    await transaction.RollbackAsync();
                                                    // Continue to the next option
                                                }
                                            }
                                        }
                                    }
                                }
                                break;
                                
                            case QuestionType.SingleChoice:
                                if (int.TryParse(entry.Value, out int singleOptionId))
                                {
                                    using (var transaction = await _context.Database.BeginTransactionAsync())
                                    {
                                        try
                                        {
                                            var answer = new PollQuestionAnswer
                                            {
                                                ResponseId = response.Id,
                                                QuestionId = questionId,
                                                SelectedOptionId = singleOptionId,
                                                TextAnswer = string.Empty,
                                                IsDeleted = false
                                            };
                                            
                                            _context.PollQuestionAnswers.Add(answer);
                                            await _context.SaveChangesAsync();
                                            await transaction.CommitAsync();
                                            successfulAnswers++;
                                        }
                                        catch
                                        {
                                            await transaction.RollbackAsync();
                                        }
                                    }
                                }
                                break;
                                
                            case QuestionType.Rating:
                                if (int.TryParse(entry.Value, out int rating))
                                {
                                    using (var transaction = await _context.Database.BeginTransactionAsync())
                                    {
                                        try
                                        {
                                            var answer = new PollQuestionAnswer
                                            {
                                                ResponseId = response.Id,
                                                QuestionId = questionId,
                                                RatingAnswer = rating,
                                                TextAnswer = string.Empty,
                                                IsDeleted = false
                                            };
                                            
                                            _context.PollQuestionAnswers.Add(answer);
                                            await _context.SaveChangesAsync();
                                            await transaction.CommitAsync();
                                            successfulAnswers++;
                                        }
                                        catch
                                        {
                                            await transaction.RollbackAsync();
                                        }
                                    }
                                }
                                break;
                                
                            case QuestionType.YesNo:
                                if (bool.TryParse(entry.Value, out bool boolValue))
                                {
                                    using (var transaction = await _context.Database.BeginTransactionAsync())
                                    {
                                        try
                                        {
                                            var answer = new PollQuestionAnswer
                                            {
                                                ResponseId = response.Id,
                                                QuestionId = questionId,
                                                BoolAnswer = boolValue,
                                                TextAnswer = string.Empty,
                                                IsDeleted = false
                                            };
                                            
                                            _context.PollQuestionAnswers.Add(answer);
                                            await _context.SaveChangesAsync();
                                            await transaction.CommitAsync();
                                            successfulAnswers++;
                                        }
                                        catch
                                        {
                                            await transaction.RollbackAsync();
                                        }
                                    }
                                }
                                break;
                                
                            case QuestionType.OpenEnded:
                                using (var transaction = await _context.Database.BeginTransactionAsync())
                                {
                                    try
                                    {
                                        // Fix: Ensure TextAnswer is properly handled
                                        var safeText = entry.Value?.Trim() ?? string.Empty;
                                        // Truncate if needed to match field constraints
                                        if (safeText.Length > 1000)
                                        {
                                            safeText = safeText.Substring(0, 1000);
                                        }
                                        
                                        var textAnswer = new PollQuestionAnswer
                                        {
                                            ResponseId = response.Id,
                                            QuestionId = questionId,
                                            TextAnswer = safeText,
                                            IsDeleted = false
                                        };
                                        
                                        _context.PollQuestionAnswers.Add(textAnswer);
                                        await _context.SaveChangesAsync();
                                        await transaction.CommitAsync();
                                        successfulAnswers++;
                                    }
                                    catch
                                    {
                                        await transaction.RollbackAsync();
                                    }
                                }
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
                    TempData["Message"] = "Thank you for your response!";
                    return RedirectToAction(nameof(Index));
                }
                else
                {
                    TempData["Error"] = $"An error occurred while submitting your response. Please try again later.";
                    return RedirectToAction(nameof(Respond), new { id = pollId });
                }
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"An error occurred while submitting your response: {ex.Message}";
                return RedirectToAction(nameof(Respond), new { id = pollId });
            }
        }

        // GET: Poll/ViewResults/5
        [Authorize(Roles = "admin")]
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
                TempData["Error"] = $"An error occurred: {ex.Message}";
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