using System.Text.Json.Nodes;
using TaigaMcp.Taiga;
using Xunit;

namespace TaigaMcp.Tests;

public class JsonBodyTests
{
    [Fact]
    public void Object_OmitsNullValues()
    {
        var obj = JsonBody.Object(("a", 1), ("b", (string?)null), ("c", "x"));

        Assert.False(obj.ContainsKey("b"));
        Assert.Equal(1, (int)obj["a"]!);
        Assert.Equal("x", (string)obj["c"]!);
    }

    [Fact]
    public void Object_SerializesDictionary()
    {
        var obj = JsonBody.Object(("points", new Dictionary<string, double> { ["5"] = 3.5 }));

        var json = obj.ToJsonString();

        Assert.Contains("\"5\":3.5", json);
    }

    [Fact]
    public void Object_DoesNotEscapeCyrillic()
    {
        var obj = JsonBody.Object(("subject", "Тест"));
        var options = new System.Text.Json.JsonSerializerOptions
        {
            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        };

        var json = obj.ToJsonString(options);

        Assert.Contains("Тест", json);
    }

    [Fact]
    public void ToNode_ClonesExistingNode()
    {
        var source = JsonNode.Parse("""{"x":1}""")!;

        var clone = JsonBody.ToNode(source)!;
        clone["x"] = 2;

        Assert.Equal(1, (int)source["x"]!);
        Assert.Equal(2, (int)clone["x"]!);
    }
}
