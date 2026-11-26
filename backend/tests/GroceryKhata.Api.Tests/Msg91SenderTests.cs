using System.Net;
using GroceryKhata.Api.Identity;
using Microsoft.Extensions.Options;

namespace GroceryKhata.Api.Tests;

/// <summary>The MSG91 sender counts a code as sent only when MSG91 says "success" (it answers 200 for refusals too).</summary>
public sealed class Msg91SenderTests
{
    private sealed class Reply(HttpStatusCode status, string body) : HttpMessageHandler
    {
        public string? Sent { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Sent = await request.Content!.ReadAsStringAsync(cancellationToken);
            return new HttpResponseMessage(status) { Content = new StringContent(body) };
        }
    }

    private static (Msg91SmsSender Sender, Reply Handler) Sender(HttpStatusCode status, string body)
    {
        var handler = new Reply(status, body);
        var options = Options.Create(new SmsOptions { Msg91AuthKey = "key", Msg91TemplateId = "tpl", Msg91OtpVariable = "OTP" });
        return (new Msg91SmsSender(new HttpClient(handler), options), handler);
    }

    [Fact]
    public async Task A_success_reply_sends_the_code_to_the_number_in_the_template_variable()
    {
        var (sender, handler) = Sender(HttpStatusCode.OK, """{"type":"success","message":"3463a1b2c3"}""");
        await sender.SendOtpAsync("+919876543210", "482613", CancellationToken.None);
        Assert.Contains("\"mobiles\":\"919876543210\"", handler.Sent);
        Assert.Contains("\"OTP\":\"482613\"", handler.Sent);
        Assert.Contains("\"template_id\":\"tpl\"", handler.Sent);
    }

    [Fact]
    public async Task A_200_that_says_error_is_a_failure_with_MSG91s_reason()
    {
        var (sender, _) = Sender(HttpStatusCode.OK, """{"type":"error","message":"Template ID is not approved"}""");
        var refused = await Assert.ThrowsAsync<HttpRequestException>(() => sender.SendOtpAsync("+919876543210", "482613", CancellationToken.None));
        Assert.Contains("Template ID is not approved", refused.Message);
    }

    [Fact]
    public async Task An_error_status_or_a_page_that_isnt_json_is_a_failure()
    {
        var (unauthorised, _) = Sender(HttpStatusCode.Unauthorized, """{"type":"error","message":"Authentication failure"}""");
        Assert.Contains("Authentication failure", (await Assert.ThrowsAsync<HttpRequestException>(() => unauthorised.SendOtpAsync("+919876543210", "1", CancellationToken.None))).Message);
        var (html, _) = Sender(HttpStatusCode.BadGateway, "<html>Bad gateway</html>");
        await Assert.ThrowsAsync<HttpRequestException>(() => html.SendOtpAsync("+919876543210", "1", CancellationToken.None));
    }
}
