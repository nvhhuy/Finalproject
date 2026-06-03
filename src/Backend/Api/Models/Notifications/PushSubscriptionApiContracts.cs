namespace Api.Models.Notifications;

public sealed record RegisterPushSubscriptionRequest(string Endpoint, PushSubscriptionKeys Keys);

public sealed record PushSubscriptionKeys(string P256dh, string Auth);

public sealed record RemovePushSubscriptionRequest(string Endpoint);
