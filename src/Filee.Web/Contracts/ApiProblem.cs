using Microsoft.AspNetCore.Http;

namespace Filee.Web.Contracts;

/// <summary>Fixed user-facing errors shared by all Web endpoints.</summary>
public static class ApiProblem
{
    public static IResult Unauthenticated() => Results.Json(
        ApiErrorEnvelope.Create("unauthenticated", "登录已失效，请重新进入应用"),
        statusCode: StatusCodes.Status401Unauthorized);

    public static IResult CsrfFailed() => Results.Json(
        ApiErrorEnvelope.Create("csrf_failed", "请求校验失败，请刷新页面重试"),
        statusCode: StatusCodes.Status400BadRequest);
}
