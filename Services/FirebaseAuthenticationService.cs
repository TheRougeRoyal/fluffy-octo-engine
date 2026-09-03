using FirebaseAdmin;
using FirebaseAdmin.Auth;
using Google.Apis.Auth.OAuth2;
using Microsoft.Extensions.Options;
using TradingEngine.Models;

namespace TradingEngine.Services;

public interface IFirebaseAuthenticationService
{
    Task<FirebaseToken> VerifyIdTokenAsync(string idToken, CancellationToken cancellationToken = default);
}

public sealed class FirebaseAuthenticationService : IFirebaseAuthenticationService
{
    private readonly FirebaseAuth _auth;

    public FirebaseAuthenticationService(IOptions<TradingServerConfig> options)
    {
        var config = options.Value;
        if (string.IsNullOrWhiteSpace(config.FirebaseServiceAccountJson))
        {
            throw new InvalidOperationException(
                "FirebaseServiceAccountJson must be configured before Firebase authentication is enabled.");
        }

        var app = FirebaseApp.Create(new AppOptions
        {
            Credential = GoogleCredential.FromJson(config.FirebaseServiceAccountJson),
            ProjectId = config.FirebaseProjectId
        });
        _auth = FirebaseAuth.GetAuth(app);
    }

    public Task<FirebaseToken> VerifyIdTokenAsync(
        string idToken,
        CancellationToken cancellationToken = default) =>
        _auth.VerifyIdTokenAsync(idToken, cancellationToken);
}
