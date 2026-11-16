using System.Security.Cryptography;

namespace Filee.Web.Jobs;

/// <summary>Web-facing job id: 128 bits of randomness encoded as 32 lower-case hex characters.</summary>
public static class WebJobId
{
    public static string New() => Convert.ToHexString(RandomNumberGenerator.GetBytes(16)).ToLowerInvariant();
}
