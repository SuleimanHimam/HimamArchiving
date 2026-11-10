namespace Archiving.Domain.Common;

/// <summary>Thrown when a domain invariant is violated (e.g. an illegal document status transition).</summary>
public sealed class DomainException(string message) : Exception(message);
