using System.Net;

namespace Shared.Errors.General;

public class ValidationError : Error
{
    public ValidationError() : base(nameof(ValidationError), (int)HttpStatusCode.BadRequest)
    {
        ValidationErrorDictionary = new Dictionary<string, string>();
    }

    public ValidationError(string key, string errorMessage) : this()
    {
        ValidationErrorDictionary.Add(key, errorMessage);
    }

    public Dictionary<string, string> ValidationErrorDictionary { get; set; }
}