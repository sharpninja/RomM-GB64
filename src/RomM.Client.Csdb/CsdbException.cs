namespace RomM.Client.Csdb;

/// <summary>Base exception for CSDb client failures.</summary>
public class CsdbException : Exception
{
    public CsdbException()
    {
    }

    public CsdbException(string message)
        : base(message)
    {
    }

    public CsdbException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}

/// <summary>Raised when a request would violate CSDb politeness policy.</summary>
public class CsdbPolitenessException : CsdbException
{
    public CsdbPolitenessException()
    {
    }

    public CsdbPolitenessException(string message)
        : base(message)
    {
    }

    public CsdbPolitenessException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
