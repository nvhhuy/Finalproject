using Application.Authentication.Common;
using Application.Contracts.Persistence;
using Application.Contracts.Services;
using Domains.UserAggregate;
using Microsoft.AspNetCore.Identity;
using Microsoft.IdentityModel.Tokens.Experimental;
using Shared.Errors;
using Shared.Errors.Authentication;

namespace Application.Authentication.Commands.Register
{
    public class RegisterCommandHandler : IRequestHandler<RegisterCommand, Result<AuthenticationResponse, Error>>
    {
        private readonly IJwtGenerator _jwtGenerator;
        private readonly IUserRespository _userRespository;
        private readonly IPasswordHasher<User> _passwordHasher;

        public RegisterCommandHandler(IJwtGenerator jwtGenerator, IUserRespository userRespository, IPasswordHasher<User> passwordHasher)
        {
            _jwtGenerator = jwtGenerator;
            _userRespository = userRespository;
            _passwordHasher = passwordHasher;
        }
        public async Task<Result<AuthenticationResponse, Error>> Handle(RegisterCommand request, CancellationToken cancellationToken)
        {
            Result<bool, ValidationError> validationResult = await request.ValidateAsync(new RegisterCommandValidator(), cancellationToken);
            if (validationResult.IsFailure)
                return validationResult.Error;


            if (await _userRespository.GetUserByEmailAsync(request.Email) != null)
            {
                return new EmailAlreadyInUseError();
            }
            if (await _userRespository.GetUserByUserNameAsync(request.UserName) != null)
            {
                return new UserNameAlreadyTakenError();
            }

            User user = User.CreateNew(request.FirstName, request.LastName, request.UserName, request.Email, request.Password);
            string hashedPassword = _passwordHasher.HashPassword(user, request.Password);
            user.UpdatePassword(hashedPassword);

            _userRespository.AddUser(user);
            await _userRespository.SaveChangesAsync();

            string token = _jwtGenerator.CreateToken(user);
            return new AuthenticationResponse(user, token);
        }
    }
}
