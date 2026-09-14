namespace Dashboard.Domain.Exceptions;

public class NotFoundException : Exception
{
    public NotFoundException(string entityName, object key)
        : base($"{entityName} با شناسه '{key}' یافت نشد.") { }
}