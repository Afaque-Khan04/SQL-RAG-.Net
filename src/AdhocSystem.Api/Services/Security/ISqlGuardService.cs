namespace AdhocSystem.Api.Services.Security;

public interface ISqlGuardService
{
    string ValidateAndSanitize(string sqlQuery);
}

public class SqlGuardSecurityException : Exception
{
    public SqlGuardSecurityException(string message) : base(message) { }
}
