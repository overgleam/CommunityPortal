using System;
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;

namespace CommunityPortal.Models.Event
{
    public class EventViewModel
    {
        public int? Id { get; set; }

        [Required(ErrorMessage = "Title is required")]
        [StringLength(100, ErrorMessage = "Title cannot be longer than 100 characters")]
        public string Title { get; set; }

        [Required(ErrorMessage = "Description is required")]
        [StringLength(2000, ErrorMessage = "Description cannot be longer than 2000 characters")]
        [DataType(DataType.MultilineText)]
        public string Description { get; set; }

        [Required(ErrorMessage = "Start date and time is required")]
        [Display(Name = "Start Date and Time")]
        [DataType(DataType.DateTime)]
        public DateTime StartDateTime { get; set; }

        [Required(ErrorMessage = "End date and time is required")]
        [Display(Name = "End Date and Time")]
        [DataType(DataType.DateTime)]
        public DateTime EndDateTime { get; set; }

        [Display(Name = "Location")]
        [StringLength(200, ErrorMessage = "Location cannot be longer than 200 characters")]
        public string? Location { get; set; }

        [Display(Name = "Maximum Attendees")]
        [Range(0, int.MaxValue, ErrorMessage = "Maximum attendees must be a positive number")]
        public int? MaxAttendees { get; set; }

        [Display(Name = "Requires Registration")]
        public bool RequiresRegistration { get; set; }

        [Display(Name = "Registration Instructions")]
        [StringLength(500, ErrorMessage = "Registration instructions cannot be longer than 500 characters")]
        public string? RegistrationInstructions { get; set; }

        [Display(Name = "Event Status")]
        public EventStatus Status { get; set; } = EventStatus.Scheduled;

        [Display(Name = "Cancellation Reason")]
        [StringLength(500, ErrorMessage = "Cancellation reason cannot be longer than 500 characters")]
        public string? CancellationReason { get; set; }

        [Display(Name = "High Priority")]
        public bool IsHighPriority { get; set; }

        [Display(Name = "Event Image")]
        public IFormFile? ImageFile { get; set; }

        public string? ExistingImageUrl { get; set; }

        [CustomValidation(typeof(EventViewModel), nameof(ValidateEventDates))]
        public static ValidationResult ValidateEventDates(EventViewModel model)
        {
            // Check if end date is after start date
            if (model.EndDateTime <= model.StartDateTime)
            {
                return new ValidationResult("End date must be after start date");
            }

            // Check if duration is not too long (e.g., more than 7 days)
            var duration = model.EndDateTime - model.StartDateTime;
            if (duration.TotalDays > 7)
            {
                return new ValidationResult("Event duration cannot exceed 7 days");
            }

            // For new events, prevent past dates
            if (model.Id == null)
            {
                if (model.StartDateTime < DateTime.Now)
                {
                    return new ValidationResult("Start date cannot be in the past for new events");
                }
            }
            // For existing events, prevent modifying past events
            else if (model.StartDateTime < DateTime.Now && model.Status != EventStatus.Cancelled && model.Status != EventStatus.Postponed)
            {
                return new ValidationResult("Cannot modify past events unless cancelling or postponing");
            }

            // Prevent events too far in the future (e.g., more than 1 year)
            if (model.StartDateTime > DateTime.Now.AddYears(1))
            {
                return new ValidationResult("Events cannot be scheduled more than 1 year in advance");
            }

            return ValidationResult.Success;
        }

        [CustomValidation(typeof(EventViewModel), nameof(ValidateRegistrationFields))]
        public static ValidationResult ValidateRegistrationFields(EventViewModel model)
        {
            if (model.RequiresRegistration)
            {
                if (model.MaxAttendees == null || model.MaxAttendees <= 0)
                {
                    return new ValidationResult("Maximum attendees must be a positive number when registration is enabled");
                }
                if (string.IsNullOrWhiteSpace(model.RegistrationInstructions))
                {
                    return new ValidationResult("Registration instructions are required when registration is enabled");
                }
                if (model.MaxAttendees > 1000)
                {
                    return new ValidationResult("Maximum attendees cannot exceed 1000");
                }
            }
            return ValidationResult.Success;
        }

        [CustomValidation(typeof(EventViewModel), nameof(ValidateCancellationReason))]
        public static ValidationResult ValidateCancellationReason(EventViewModel model)
        {
            if (model.Status == EventStatus.Cancelled)
            {
                if (string.IsNullOrWhiteSpace(model.CancellationReason))
                {
                    return new ValidationResult("Cancellation reason is required when cancelling an event");
                }
                if (model.CancellationReason.Length < 10)
                {
                    return new ValidationResult("Cancellation reason must be at least 10 characters long");
                }
            }
            return ValidationResult.Success;
        }
    }
} 