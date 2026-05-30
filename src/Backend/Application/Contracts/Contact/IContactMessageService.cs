namespace Application.Contracts.Contact;

public interface IContactMessageService
{
    Task<Guid> SubmitAsync(
        string name,
        string email,
        string subject,
        string body,
        CancellationToken cancellationToken = default);

    Task<bool> MarkReadAsync(Guid messageId, CancellationToken cancellationToken = default);
}
