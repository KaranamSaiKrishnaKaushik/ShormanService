namespace ShormanServicesBackend.Api.Security;

public class Auth0Options
{
    public const string SectionName = "Auth0";

    public string Domain { get; set; } = string.Empty;
    public string Audience { get; set; } = string.Empty;
}

public static class AuthSchemes
{
    public const string AppJwt = "AppJwt";
    public const string Auth0 = "Auth0";
}