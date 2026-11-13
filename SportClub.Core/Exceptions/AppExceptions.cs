namespace SportClub.Core.Exceptions;

/// <summary>Base class for business errors. The API turns them into ProblemDetails.</summary>
public abstract class AppException : Exception
{
    protected AppException(string message, string title) : base(message) => Title = title;
    public string Title { get; }
    public abstract int StatusCode { get; }
}

public class NotFoundException : AppException
{
    public NotFoundException(string what, object id) : base($"{what} (id {id}) ei leitud.", "Ei leitud") { }
    public override int StatusCode => 404;
}

public class ConflictException : AppException
{
    public ConflictException(string message, string title = "Konflikt") : base(message, title) { }
    public override int StatusCode => 409;
}

/// <summary>The session has no free spots (HTTP 409).</summary>
public class SessionFullException : ConflictException
{
    public SessionFullException(int sessionId)
        : base($"Treening {sessionId} on täis. Võid end panna ootejärjekorda.", "Treening on täis") { }
}

public class BusinessRuleException : AppException
{
    public BusinessRuleException(string message, string title = "Vigane päring") : base(message, title) { }
    public override int StatusCode => 400;
}

public class ForbiddenException : AppException
{
    public ForbiddenException(string message) : base(message, "Õigused puuduvad") { }
    public override int StatusCode => 403;
}
