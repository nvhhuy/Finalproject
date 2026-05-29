using Application.Common.Behaviors;
using Application.Contracts.Authorization;
using Application.Contracts.Services;
using Domains.UserAggregate;
using Domains.UserAggregate.ValueObjects;
using MediatR;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Shared.Common;
using Shared.Errors;
using Shared.Errors.General;

namespace Application.Tests;

/// <summary>
///     Unit tests for <see cref="AdminAuthorizationBehavior{TRequest,TResponse}" />. These verify that
///     the marker-interface defence-in-depth check short-circuits non-admin callers without invoking
///     the downstream handler and lets admins through unmodified.
/// </summary>
public sealed class AdminAuthorizationBehaviorTests
{
    [Fact]
    public async Task Returns_forbidden_when_caller_is_not_in_admin_role()
    {
        var currentUser = new Mock<ILoggedInUserService>();
        currentUser.SetupGet(u => u.UserId).Returns(UserId.CreateNew());
        currentUser.Setup(u => u.IsInRole(UserRoles.Admin)).Returns(false);

        var behavior = new AdminAuthorizationBehavior<DummyAdminRequest, Result<string, Error>>(
            currentUser.Object,
            NullLogger<AdminAuthorizationBehavior<DummyAdminRequest, Result<string, Error>>>.Instance);

        var handlerCalled = false;
        RequestHandlerDelegate<Result<string, Error>> next = _ =>
        {
            handlerCalled = true;
            return Task.FromResult<Result<string, Error>>("ok");
        };

        var result = await behavior.Handle(new DummyAdminRequest("p"), next, CancellationToken.None);

        Assert.False(handlerCalled);
        Assert.True(result.IsFailure);
        Assert.IsType<ForbiddenError>(result.Error);
    }

    [Fact]
    public async Task Returns_forbidden_when_caller_is_anonymous()
    {
        var currentUser = new Mock<ILoggedInUserService>();
        currentUser.SetupGet(u => u.UserId).Returns((UserId?)null);
        currentUser.Setup(u => u.IsInRole(It.IsAny<string>())).Returns(false);

        var behavior = new AdminAuthorizationBehavior<DummyAdminRequest, Result<string, Error>>(
            currentUser.Object,
            NullLogger<AdminAuthorizationBehavior<DummyAdminRequest, Result<string, Error>>>.Instance);

        RequestHandlerDelegate<Result<string, Error>> blowUp = _ => Task.FromResult<Result<string, Error>>("never");
        var result = await behavior.Handle(new DummyAdminRequest("p"), blowUp, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.IsType<ForbiddenError>(result.Error);
    }

    [Fact]
    public async Task Forwards_to_handler_when_caller_is_admin()
    {
        var currentUser = new Mock<ILoggedInUserService>();
        currentUser.SetupGet(u => u.UserId).Returns(UserId.CreateNew());
        currentUser.Setup(u => u.IsInRole(UserRoles.Admin)).Returns(true);

        var behavior = new AdminAuthorizationBehavior<DummyAdminRequest, Result<string, Error>>(
            currentUser.Object,
            NullLogger<AdminAuthorizationBehavior<DummyAdminRequest, Result<string, Error>>>.Instance);

        RequestHandlerDelegate<Result<string, Error>> next = _ =>
            Task.FromResult<Result<string, Error>>("handler-output");
        var result = await behavior.Handle(new DummyAdminRequest("payload"), next, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("handler-output", result.Value);
    }

    private sealed record DummyAdminRequest(string Payload) : IRequest<Result<string, Error>>, IRequireAdmin;
}