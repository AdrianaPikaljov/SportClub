namespace SportClub.Core.Exceptions;

/// <summary>Äriloogika viga. API teeb sellest ProblemDetails vastuse.</summary>
public abstract class AppException : Exception
{
    protected AppException(string message, string title) : base(message) => Title = title;
    public string Title { get; }
    public abstract int StatusCode { get; }
}

/// <summary>404 – ei leitud.</summary>
public class NotFoundException : AppException
{
    public NotFoundException(string what, object id) : base($"{what} (id {id}) ei leitud.", "Ei leitud") { }
    public NotFoundException(string message) : base(message, "Ei leitud") { }
    public override int StatusCode => 404;
}

/// <summary>409 – konflikt (nt juba olemas).</summary>
public class ConflictException : AppException
{
    public ConflictException(string message, string title = "Konflikt") : base(message, title) { }
    public override int StatusCode => 409;
}

/// <summary>409 – treening on täis.</summary>
public class SessionFullException : ConflictException
{
    public SessionFullException(int sessionId, bool waitlistClosed = false)
        : base(waitlistClosed
                ? $"Treening {sessionId} on täis ja algab vähem kui tunni pärast. Ootejärjekorda enam ei saa."
                : $"Treening {sessionId} on täis.",
            "Treening on täis") { }
}

/// <summary>400 – reegel ei luba (nt kuukaart puudub, treening on möödas).</summary>
public class BusinessRuleException : AppException
{
    public BusinessRuleException(string message, string title = "Vigane päring") : base(message, title) { }
    public override int StatusCode => 400;
}

/// <summary>402 – makse lükati tagasi (nt testkaart 4000 0000 0000 0002).</summary>
public class PaymentDeclinedException : AppException
{
    public PaymentDeclinedException(string message) : base(message, "Makse ebaõnnestus") { }
    public override int StatusCode => 402;
}

/// <summary>403 – pole õigust (nt teise kliendi broneering).</summary>
public class ForbiddenException : AppException
{
    public ForbiddenException(string message) : base(message, "Õigused puuduvad") { }
    public override int StatusCode => 403;
}
