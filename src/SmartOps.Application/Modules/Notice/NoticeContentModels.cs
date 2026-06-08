namespace SmartOps.Application.Modules.Notice;

public sealed class NoticeFormQuestionDto
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    public string Label { get; set; } = string.Empty;

    /// <summary>text | yesno | number | mcq_single | mcq_multi | poll</summary>
    public string Type { get; set; } = "text";

    public bool Required { get; set; }

    public IList<NoticeQuestionOptionDto> Options { get; set; } = [];

    /// <summary>For MCQ: whether to validate answers</summary>
    public bool HasAnswerKey { get; set; }

    /// <summary>For MCQ: option ids which are correct</summary>
    public IList<string> CorrectOptionIds { get; set; } = [];
}

public sealed class NoticeQuestionOptionDto
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    public string Label { get; set; } = string.Empty;
}

public sealed class NoticeContentPayloadDto
{
    public IList<NoticeFormQuestionDto> Questions { get; set; } = [];

    public string? DocumentName { get; set; }

    public string? DocumentUrl { get; set; }

    public string? FeeMessageTemplate { get; set; }

    public IList<Guid> TargetRecipientIds { get; set; } = [];
}

public sealed class NoticeAudienceOptionDto
{
    public Guid Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public string? Subtitle { get; set; }
}

public sealed class NoticeAudiencePreviewDto
{
    public int EstimatedRecipients { get; set; }

    public IList<NoticeAudienceOptionDto> Options { get; set; } = [];
}
