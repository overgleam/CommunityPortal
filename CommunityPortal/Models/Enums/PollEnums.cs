namespace CommunityPortal.Models.Enums
{
    public enum PollStatus
    {
        Draft,
        Published,
        Closed,
        Archived
    }

    public enum PollTargetAudience
    {
        AllHomeowners,
        SpecificHomeowners
    }

    public enum QuestionType
    {
        MultipleChoice,
        SingleChoice,
        Rating,
        OpenEnded,
        YesNo
    }
} 