# Security Policy

## Supported Versions

We release patches for security vulnerabilities for the following versions:

| Version | Supported          |
| ------- | ------------------ |
| 1.0.x   | :white_check_mark: |
| < 1.0   | :x:                |

## Reporting a Vulnerability

We take the security of SportsbookLite seriously. If you have discovered a security vulnerability in our project, we appreciate your help in disclosing it to us in a responsible manner.

### Reporting Process

**Please do not report security vulnerabilities through public GitHub issues.**

Instead, please report them via email to:
- Primary: security@sportsbook-lite.com
- Backup: maintainers@sportsbook-lite.com

Please include the following information:

- Type of issue (e.g., buffer overflow, SQL injection, cross-site scripting, etc.)
- Full paths of source file(s) related to the manifestation of the issue
- The location of the affected source code (tag/branch/commit or direct URL)
- Any special configuration required to reproduce the issue
- Step-by-step instructions to reproduce the issue
- Proof-of-concept or exploit code (if possible)
- Impact of the issue, including how an attacker might exploit it

### Response Timeline

- **Initial Response**: Within 48 hours
- **Status Update**: Within 5 business days
- **Resolution Timeline**: Depends on severity
  - Critical: 7 days
  - High: 14 days
  - Medium: 30 days
  - Low: 60 days

## Security Best Practices

### For Contributors

When contributing to SportsbookLite, please ensure:

#### 1. Dependency Management
```xml
<!-- Keep dependencies updated -->
<PackageReference Include="Microsoft.Orleans.Server" Version="9.2.1" />
<!-- Use dependabot or similar for automatic updates -->
```

#### 2. Secret Management
```csharp
// Never hardcode secrets
// BAD ❌
var connectionString = "Server=localhost;Password=MySecretPassword123";

// GOOD ✅
var connectionString = configuration.GetConnectionString("Database");
```

#### 3. Input Validation
```csharp
// Always validate input
public async ValueTask<BetResult> PlaceBetAsync(PlaceBetRequest request)
{
    // Validate input
    ArgumentNullException.ThrowIfNull(request);

    if (request.Amount <= 0 || request.Amount > 10000)
        throw new ArgumentException("Invalid bet amount");

    // Sanitize string inputs
    request.UserId = request.UserId.Trim();

    // Continue processing...
}
```

#### 4. SQL Injection Prevention
```csharp
// Use parameterized queries
// BAD ❌
var query = $"SELECT * FROM users WHERE id = {userId}";

// GOOD ✅
var query = "SELECT * FROM users WHERE id = @userId";
var parameters = new { userId };
```

#### 5. Authentication & Authorization
```csharp
// Use proper authentication
[Authorize]
[EndpointGroup("bets")]
public class PlaceBetEndpoint : Endpoint<PlaceBetRequest, BetResult>
{
    public override void Configure()
    {
        Post("/api/bets");
        Policies("RequireAuthenticatedUser");
    }
}
```

### For Deployments

#### 1. Environment Configuration
```yaml
# Use secrets management
apiVersion: v1
kind: Secret
metadata:
  name: sportsbook-secrets
type: Opaque
data:
  database-password: <base64-encoded>
  pulsar-token: <base64-encoded>
```

#### 2. Network Security
```yaml
# Implement network policies
apiVersion: networking.k8s.io/v1
kind: NetworkPolicy
metadata:
  name: orleans-silo-policy
spec:
  podSelector:
    matchLabels:
      app: orleans-silo
  policyTypes:
  - Ingress
  - Egress
  ingress:
  - from:
    - podSelector:
        matchLabels:
          app: api
    ports:
    - protocol: TCP
      port: 11111
```

#### 3. TLS Configuration
```csharp
// Enforce HTTPS
builder.Services.AddHttpsRedirection(options =>
{
    options.RedirectStatusCode = StatusCodes.Status307TemporaryRedirect;
    options.HttpsPort = 443;
});

// Use HSTS
builder.Services.AddHsts(options =>
{
    options.Preload = true;
    options.IncludeSubDomains = true;
    options.MaxAge = TimeSpan.FromDays(365);
});
```

## Security Features

### Current Security Measures

1. **Authentication**: JWT-based authentication with refresh tokens
2. **Authorization**: Role-based access control (RBAC)
3. **Data Protection**: Encryption at rest and in transit
4. **Rate Limiting**: Per-user and per-IP rate limits
5. **Input Validation**: Comprehensive validation using FluentValidation
6. **Audit Logging**: All sensitive operations are logged
7. **Secrets Management**: Using Azure Key Vault / AWS Secrets Manager

### Orleans-Specific Security

```csharp
// Grain authorization
public class SecureBetGrain : Grain, IBetGrain
{
    public async ValueTask<BetResult> PlaceBetAsync(PlaceBetRequest request)
    {
        // Verify caller identity
        var identity = RequestContext.Get("UserIdentity") as string;
        if (string.IsNullOrEmpty(identity))
            throw new UnauthorizedAccessException();

        // Validate permissions
        if (!await HasPermissionAsync(identity, "PlaceBet"))
            throw new ForbiddenException();

        // Process request...
    }
}
```

### Pulsar Security

```csharp
// Configure Pulsar with authentication
services.AddPulsar(options =>
{
    options.ServiceUrl = "pulsar+ssl://pulsar:6651";
    options.Authentication = new AuthenticationToken(
        configuration["Pulsar:Token"]);
    options.TlsTrustCertificate = "/path/to/ca.cert";
});
```

## Known Security Considerations

### Distributed Systems Security

1. **Inter-silo communication**: Secured via mutual TLS
2. **Grain persistence**: Encrypted storage providers
3. **Event streaming**: Authenticated Pulsar connections
4. **Cache poisoning**: Redis ACL and password protection

### API Security

1. **CORS**: Properly configured for production domains
2. **CSRF**: Token-based protection for state-changing operations
3. **XSS**: Content Security Policy headers implemented
4. **Clickjacking**: X-Frame-Options configured

## Security Checklist for Production

- [ ] All secrets in secure vault (not in code/config)
- [ ] TLS/SSL certificates valid and not self-signed
- [ ] Database connections use SSL
- [ ] Redis password protected
- [ ] Pulsar authentication enabled
- [ ] Rate limiting configured
- [ ] CORS properly configured
- [ ] Security headers implemented
- [ ] Audit logging enabled
- [ ] Monitoring and alerting configured
- [ ] Regular dependency updates scheduled
- [ ] Security scanning in CI/CD pipeline

## Additional Resources

- [OWASP Top 10](https://owasp.org/www-project-top-ten/)
- [Orleans Security Documentation](https://docs.microsoft.com/en-us/dotnet/orleans/security/)
- [.NET Security Best Practices](https://docs.microsoft.com/en-us/aspnet/core/security/)
- [Pulsar Security](https://pulsar.apache.org/docs/en/security-overview/)

## Vulnerability Disclosure Policy

We follow a coordinated disclosure policy:

1. Reporter submits vulnerability
2. We acknowledge receipt within 48 hours
3. We investigate and develop a fix
4. We notify reporter when fix is ready
5. We release the fix
6. We publicly disclose the vulnerability (crediting reporter if desired)

## Contact

For any security concerns, please contact:
- Email: security@sportsbook-lite.com
- GPG Key: [Public key available here]

---

**Thank you for helping keep SportsbookLite and its users safe!**