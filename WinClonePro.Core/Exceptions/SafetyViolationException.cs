using System;

namespace WinClonePro.Core.Exceptions;

public class SafetyViolationException : Exception
{
    public SafetyViolationException()
    {
    }

    public SafetyViolationException(string message) : base(message)
    {
    }

    public SafetyViolationException(string message, Exception innerException) : base(message, innerException)
    {
    }
}

