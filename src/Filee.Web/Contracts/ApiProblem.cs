using Microsoft.AspNetCore.Http;

namespace Filee.Web.Contracts;

/// <summary>Fixed user-facing errors shared by all Web endpoints.</summary>
public static class ApiProblem
{
    public static IResult Unauthenticated() => Problem("unauthenticated", "登录已失效，请重新进入应用", StatusCodes.Status401Unauthorized);

    public static IResult CsrfFailed() => Problem("csrf_failed", "请求校验失败，请刷新页面重试", StatusCodes.Status400BadRequest);

    public static IResult InvalidTarget() => Problem("invalid_target", "不支持该目标格式", StatusCodes.Status400BadRequest);

    public static IResult UnsupportedOption() => Problem("unsupported_option", "当前版本不支持自定义转换选项", StatusCodes.Status400BadRequest);

    public static IResult FileTooLarge() => Problem("file_too_large", "文件超过大小上限", StatusCodes.Status400BadRequest);

    public static IResult BadMagic() => Problem("bad_magic", "文件内容与扩展名不符", StatusCodes.Status400BadRequest);

    public static IResult NotFound() => Problem("not_found", "任务不存在", StatusCodes.Status404NotFound);

    public static IResult AlreadyFinished() => Problem("already_finished", "任务已结束", StatusCodes.Status409Conflict);

    public static IResult Forbidden() => Problem("forbidden", "无权访问该资源", StatusCodes.Status403Forbidden);

    public static IResult InvalidToken() => Problem("invalid_token", "下载链接无效", StatusCodes.Status403Forbidden);

    public static IResult Expired() => Problem("expired", "下载链接已过期", StatusCodes.Status410Gone);

    private static IResult Problem(string code, string message, int status) =>
        Results.Json(ApiErrorEnvelope.Create(code, message), statusCode: status);
}
