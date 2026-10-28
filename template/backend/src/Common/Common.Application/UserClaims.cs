namespace Common.Application;

/// <summary>The names of the claims that describe the signed-in user, shared by every layer that reads them.</summary>
public static class UserClaims
{
    /// <summary>The user's stable id: the identity provider's subject, never an email.</summary>
    public const string Id = "sub";

    /// <summary>One claim per permission the user holds.</summary>
    public const string Permission = "permission";
}
