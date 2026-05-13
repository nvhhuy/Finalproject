using Api.Models;
using Application.Modules.Users.Administration.Commands;
using Api.Models.Admin;
using Microsoft.AspNetCore.Mvc;
using Shared.Common;
using Shared.Errors;
using Shared.Errors.General;

namespace Api.Controllers.Samples;

public sealed partial class SamplesController
{
    /// <summary>Explicit transaction: begin, command (SaveChanges), commit or rollback.</summary>
    [HttpPost("transactions/manual-lock/{userId:guid}")]
    public async Task<IActionResult> ManualLockUser(Guid userId, [FromBody] LockUserRequest? body, CancellationToken cancellationToken)
    {
        await _unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            Result<bool, Error> result = await Sender.Send(new AdminLockUserCommand(userId, body?.Reason, CurrentAdminId), cancellationToken);
            if (result.IsFailure)
            {
                await _unitOfWork.RollbackAsync(cancellationToken);
                if (result.Error is ValidationError ve)
                    return BadRequestValidationApi(ve);
                return FailApi(result.Error);
            }

            await _unitOfWork.CommitAsync(cancellationToken);
            return OkApiEmpty();
        }
        catch
        {
            await _unitOfWork.RollbackAsync(cancellationToken);
            throw;
        }
    }

    [HttpPost("transactions/empty-roundtrip")]
    public async Task<IActionResult> EmptyTransactionRoundTrip(CancellationToken cancellationToken)
    {
        await _unitOfWork.BeginTransactionAsync(cancellationToken);
        await _unitOfWork.CommitAsync(cancellationToken);
        return OkApiEmpty();
    }
}
