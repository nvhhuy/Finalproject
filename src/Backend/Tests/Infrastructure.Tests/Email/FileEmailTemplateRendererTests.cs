using Application.Common.Email;
using Application.Modules.Users.Authentication.Options;
using Infrastructure.Email;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Infrastructure.Tests.Email;

public sealed class FileEmailTemplateRendererTests
{
    [Fact]
    public void Render_replaces_tokens_and_encodes_greeting()
    {
        var templatesDir = Path.Combine(AppContext.BaseDirectory, "Email", "Templates");

        var host = new FakeHostEnvironment(templatesDir);
        var renderer = new FileEmailTemplateRenderer(
            Options.Create(new EmailTemplateOptions { TemplatesDirectory = templatesDir }),
            host,
            NullLogger<FileEmailTemplateRenderer>.Instance);

        var html = renderer.Render(
            EmailTemplateNames.VerifyEmail,
            EmailTemplateTokenBuilder.ForVerifyEmail(
                "Test App",
                "http://localhost/verify?tokenId=abc&token=def",
                30,
                recipientName: "<script>alert(1)</script>"));

        Assert.Contains("Test App", html);
        Assert.Contains("Hi &lt;script&gt;alert(1)&lt;/script&gt;,", html);
        Assert.Contains("http://localhost/verify?tokenId=abc&token=def", html);
        Assert.DoesNotContain("[[greetingName]]", html);
    }

    private sealed class FakeHostEnvironment : IHostEnvironment
    {
        public FakeHostEnvironment(string contentRoot) => ContentRootPath = contentRoot;

        public string EnvironmentName { get; set; } = "Development";
        public string ApplicationName { get; set; } = "Tests";
        public string ContentRootPath { get; set; }
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
