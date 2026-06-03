namespace Application.Modules.Users.Authentication.Options;

public sealed class EmailTemplateOptions
{
    public const string SectionName = "EmailTemplates";

    /// <summary>Folder containing <c>*.html</c> templates (relative to app base directory or absolute).</summary>
    public string TemplatesDirectory { get; set; } = "Email/Templates";
}
