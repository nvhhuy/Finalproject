using Application.Authentication.Common;
using Application.Contracts.Persistence;
using Application.Contracts.Services;
using Application.Extensions;
using Domains.UserAggregate;
using Microsoft.AspNetCore.Identity;
using Microsoft.IdentityModel.Tokens.Experimental;
using Shared.Errors.Authentication;


namespace Application.Authentication.Queries 
{
    public class LoginQueryHandler : IRequestHandler<LoginQuery, Result<AuthenticationResponse, Error>>
    {
        private readonly IJwtGenerator _jwtGenerator;
        private readonly IUserRespository _userRespository;
        private readonly IPasswordHasher<User> _passwordHasher;

        public LoginQueryHandler(IJwtGenerator jwtGenerator, IUserRespository userRespository, IPasswordHasher<User> passwordHasher)
        {
            _jwtGenerator = jwtGenerator;
            _userRespository = userRespository;
            _passwordHasher = passwordHasher;
        }
        public async Task<Result<AuthenticationResponse, Error>> Handle(LoginQuery request, CancellationToken cancellationToken)
        {
            Result<bool, ValidationError> validationResult = await request.ValidateAsync(new LoginQueryValidator(), cancellationToken);
            if (validationResult.IsFailure)
                return validationResult.Error;

            User? user = await _userRespository.GetUserByEmailAsync(request.Email);
            if (user is null)
                return new InvalidCredentialsError("Invalid email or password");

            PasswordVerificationResult passwordVerificationResult = _passwordHasher.VerifyHashedPassword(user, user.Password, request.Password);

            if (passwordVerificationResult == PasswordVerificationResult.Failed)
                return new InvalidCredentialsError("Invalid email or password");

            string token = _jwtGenerator.CreateToken(user);

            return new AuthenticationResponse(user, token);


        }
    }
}
