using System.Text.Json.Nodes;
using BotSharp.Plugin.FloxiAI.Providers.Chat;
using Shouldly;

namespace BotSharp.LLM.Tests;

public class FloxiTextToolCallTests
{
    // As a streamed hang-up came back from the backend, cut off inside its last parameter
    private const string CutOffHangup = "<tool_call>\n<function=util-twilio-hangup_phone_call>\n<parameter=reason>\n"
        + "Caller has no further requests and is ending the call.\n</parameter>\n<parameter=response_content>\nAlrig";

    [Fact]
    public void ReadsTheCallWithOnlyItsClosedParameters()
    {
        TextToolCall.TryParse(CutOffHangup, out var name, out var arguments, out var before).ShouldBeTrue();

        name.ShouldBe("util-twilio-hangup_phone_call");
        before.ShouldBeEmpty();
        var args = JsonNode.Parse(arguments)!.AsObject();
        args["reason"]!.GetValue<string>().ShouldBe("Caller has no further requests and is ending the call.");
        args.ContainsKey("response_content").ShouldBeFalse();
    }

    [Fact]
    public void KeepsJsonValuesAndTheTextBeforeTheCall()
    {
        TextToolCall.TryParse("Okay.\n<tool_call><function=answer_question><parameter=answers>{\"D1\": \"Yes\"}</parameter>"
            + "<parameter=unclear>false</parameter><parameter=contact_phone>6755550118</parameter></function></tool_call>",
            out var name, out var arguments, out var before).ShouldBeTrue();

        name.ShouldBe("answer_question");
        before.ShouldBe("Okay.");
        var args = JsonNode.Parse(arguments)!.AsObject();
        args["answers"]!["D1"]!.GetValue<string>().ShouldBe("Yes");
        args["unclear"]!.GetValue<bool>().ShouldBeFalse();
        args["contact_phone"]!.GetValue<string>().ShouldBe("6755550118");
    }

    [Theory]
    [InlineData("Is there anything else?")]
    [InlineData("")]
    [InlineData(null)]
    [InlineData("<tool_call> with no function")]
    public void FindsNoCallInPlainText(string? text)
    {
        TextToolCall.TryParse(text, out _, out _, out _).ShouldBeFalse();
    }

    [Theory]
    [InlineData("Is there anything else?", 23)]
    [InlineData("Okay. <", 6)]
    [InlineData("Okay. <tool_", 6)]
    [InlineData("Okay. <tool_call>\n<function=x>", 6)]
    [InlineData("a < b", 5)]
    [InlineData("<tool_call>", 0)]
    public void StreamsNothingFromWhereACallStartsOrMayStart(string text, int streamable)
    {
        TextToolCall.StreamableLength(text).ShouldBe(streamable);
    }
}
