namespace TechMove.Api.Services;

/// <summary>Base type for expected, user-facing errors. Controllers show these as validation messages.</summary>
public abstract class TechMoveException : Exception
{
    protected TechMoveException(string message, Exception? inner = null) : base(message, inner) { }
}

/// <summary>A business rule was broken (e.g. service request on an Expired contract).</summary>
public class BusinessRuleException : TechMoveException
{
    public BusinessRuleException(string message) : base(message) { }
}

/// <summary>An uploaded file failed validation (wrong type, empty, too large, not a real PDF).</summary>
public class InvalidFileException : TechMoveException
{
    public InvalidFileException(string message) : base(message) { }
}

/// <summary>An external dependency (the currency API) is unavailable.</summary>
public class ExternalServiceException : TechMoveException
{
    public ExternalServiceException(string message, Exception? inner = null) : base(message, inner) { }
}

/// <summary>A record that the user asked for does not exist.</summary>
public class NotFoundException : TechMoveException
{
    public NotFoundException(string message) : base(message) { }
}
