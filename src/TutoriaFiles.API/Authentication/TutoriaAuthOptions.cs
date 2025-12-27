using Microsoft.AspNetCore.Authentication;

namespace TutoriaFiles.API.Authentication;

public class TutoriaAuthOptions : AuthenticationSchemeOptions
{
    public const string SchemeName = "TutoriaBearer";
}
