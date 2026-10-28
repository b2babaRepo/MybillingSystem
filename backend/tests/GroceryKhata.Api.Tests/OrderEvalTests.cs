using System.Text.Json;
using Anthropic;
using GroceryKhata.Api.Catalogue;
using GroceryKhata.Api.Planner;
using Xunit.Abstractions;

namespace GroceryKhata.Api.Tests;

/// <summary>
/// AI phase 3: how well orders are read, measured on a fixed test set (Data/order-eval.json). An item counts when the
/// right common-list item comes back; a quantity counts when the amount and unit are right too. The rule reader must
/// not fall below its floor; the Claude reader runs only when ANTHROPIC_API_KEY is set (it costs money) and prints its
/// score for comparison before a model or prompt change goes live.
/// </summary>
public sealed class OrderEvalTests(ITestOutputHelper output)
{
    private sealed record Case(string Text, List<JsonElement[]> Expect);

    private sealed record EvalSet(List<Case> Cases);

    [Fact]
    public async Task The_rule_reader_keeps_its_accuracy_on_the_test_set()
    {
        var (items, quantities) = await ScoreAsync(new RuleOrderReader());

        Assert.True(items >= 0.9, $"Item accuracy {items:P0} fell below 90%");
        Assert.True(quantities >= 0.85, $"Quantity accuracy {quantities:P0} fell below 85%");
    }

    [Fact]
    public async Task The_claude_reader_scores_on_the_test_set_when_a_key_is_set()
    {
        var key = Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY");
        if (string.IsNullOrWhiteSpace(key))
        {
            output.WriteLine("ANTHROPIC_API_KEY not set: Claude eval skipped.");
            return;
        }

        var model = Environment.GetEnvironmentVariable("AI_EVAL_MODEL") ?? "claude-opus-5-5";
        var (items, quantities) = await ScoreAsync(new ClaudeOrderReader(new AnthropicClient { ApiKey = key }, model));
        Assert.True(items >= 0.9, $"{model}: item accuracy {items:P0}");
        Assert.True(quantities >= 0.85, $"{model}: quantity accuracy {quantities:P0}");
    }

    private async Task<(double Items, double Quantities)> ScoreAsync(IOrderReader reader)
    {
        var set = JsonSerializer.Deserialize<EvalSet>(
            await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory, "Data", "order-eval.json")),
            new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
        int expected = 0, itemHits = 0, quantityHits = 0, extras = 0;
        foreach (var c in set.Cases)
        {
            var read = (await reader.ReadAsync(new OrderInput(c.Text, null, null), default))
                .Select(OrderDrafts.Check).OfType<DraftLineDto>().Where(l => l.CatalogueItemId is not null).ToList();
            foreach (var e in c.Expect)
            {
                expected++;
                var id = CommonCatalogue.Items.Single(i => i.NameEn == e[0].GetString()).Id;
                var line = read.FirstOrDefault(l => l.CatalogueItemId == id);
                var unit = Enum.Parse<Unit>(e[2].GetString()!, ignoreCase: true);
                if (line is null)
                {
                    output.WriteLine($"MISS item   \"{c.Text}\" → {e[0]}");
                    continue;
                }

                itemHits++;
                if (line.Quantity == e[1].GetDecimal() && line.Unit == unit)
                {
                    quantityHits++;
                }
                else
                {
                    output.WriteLine($"MISS amount \"{c.Text}\" → {e[0]}: got {line.Quantity} {line.Unit}, want {e[1]} {unit}");
                }
            }

            extras += read.Count(l => c.Expect.All(e => CommonCatalogue.Items.Single(i => i.NameEn == e[0].GetString()).Id != l.CatalogueItemId));
        }

        double items = (double)itemHits / expected, quantities = (double)quantityHits / expected;
        output.WriteLine($"{reader.Name}: items {items:P0}, quantities {quantities:P0}, extra items {extras} ({expected} expected items)");
        return (items, quantities);
    }
}
