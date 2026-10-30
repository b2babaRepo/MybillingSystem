using System.Net;
using System.Net.Http.Json;
using GroceryKhata.Api.Teams;

namespace GroceryKhata.Api.Tests;

/// <summary>Team kharcha: a shared book several app users write into, adding up by name, day and month.</summary>
[Collection(ApiCollection.Name)]
public sealed class TeamKharchaTests(ApiFactory api)
{
    [Fact]
    public async Task People_join_with_a_code_write_their_own_lines_and_see_the_book_by_name()
    {
        var (ravi, _) = await api.SignUpAsync("Ravi's shop");
        Assert.Equal(HttpStatusCode.BadRequest, (await ravi.PostJsonAsync("/api/v1/teams", new { name = "Mishra family", consent = false })).StatusCode);
        var team = await (await ravi.PostJsonAsync("/api/v1/teams", new { name = "Mishra family", yourName = "Ravi", consent = true })).ReadAsync<TeamDto>();
        Assert.Equal(TeamRole.Owner, team.MyRole);

        team = await (await ravi.PostJsonAsync($"/api/v1/teams/{team.Id}/invite", new { })).ReadAsync<TeamDto>();
        Assert.Matches("^[A-Z2-9]{8}$", team.InviteCode);

        // Sunita (a household account) sees the team before joining, and must agree to sharing.
        var (sunita, _) = await api.SignUpAsync("Sunita's shop");
        var preview = await sunita.GetJsonAsync<TeamPreview>($"/api/v1/teams/preview?code={team.InviteCode!.ToLowerInvariant()}");
        Assert.Equal(("Mishra family", 1), (preview.Name, preview.Members));
        Assert.Equal(HttpStatusCode.BadRequest, (await sunita.PostJsonAsync("/api/v1/teams/join", new { code = team.InviteCode, consent = false })).StatusCode);
        var joined = await (await sunita.PostJsonAsync("/api/v1/teams/join", new { code = team.InviteCode, yourName = "Sunita", consent = true })).ReadAsync<TeamDto>();
        Assert.Equal((TeamRole.Member, 2), (joined.MyRole, joined.Members.Count));

        // The owner adds Dadi, who has no phone, and writes for her.
        team = await (await ravi.PostJsonAsync($"/api/v1/teams/{team.Id}/members", new { name = "Dadi" })).ReadAsync<TeamDto>();
        var dadi = team.Members.Single(m => m.Name == "Dadi");
        Assert.False(dadi.HasLogin);

        Assert.Equal(HttpStatusCode.Created, (await ravi.PostJsonAsync($"/api/v1/teams/{team.Id}/expenses", new { id = Guid.CreateVersion7(), amount = 500m, note = "bijli", mode = "upi" })).StatusCode);
        Assert.Equal(HttpStatusCode.Created, (await ravi.PostJsonAsync($"/api/v1/teams/{team.Id}/expenses", new { id = Guid.CreateVersion7(), amount = 200m, note = "dawai", memberId = dadi.Id })).StatusCode);
        var sabzi = Guid.CreateVersion7();
        Assert.Equal(HttpStatusCode.Created, (await sunita.PostJsonAsync($"/api/v1/teams/{team.Id}/expenses", new { id = sabzi, amount = 300m, note = "sabzi" })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await sunita.PostJsonAsync($"/api/v1/teams/{team.Id}/expenses", new { id = sabzi, amount = 300m, note = "sabzi" })).StatusCode); // sent twice: once

        // A member writes only for themself.
        Assert.Equal(HttpStatusCode.BadRequest, (await sunita.PostJsonAsync($"/api/v1/teams/{team.Id}/expenses", new { id = Guid.CreateVersion7(), amount = 50m, memberId = dadi.Id })).StatusCode);

        var day = await sunita.GetJsonAsync<TeamDayDto>($"/api/v1/teams/{team.Id}/day");
        Assert.Equal(1000m, day.Total);
        Assert.Equal(["Ravi 500", "Sunita 300", "Dadi 200"], day.ByName.Select(n => $"{n.Name} {n.Total:0}"));

        // A strike takes the line out of the totals; a member can't strike someone else's line.
        var bijli = day.Lines.Single(l => l.Note == "bijli");
        Assert.False(bijli.CanStrike);
        Assert.Equal(HttpStatusCode.Forbidden, (await sunita.PostJsonAsync($"/api/v1/teams/{team.Id}/expenses/{bijli.Id}/strike", new { reason = "x" })).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await sunita.PostJsonAsync($"/api/v1/teams/{team.Id}/expenses/{sabzi}/strike", new { reason = "double" })).StatusCode);

        var month = await ravi.GetJsonAsync<TeamMonthDto>($"/api/v1/teams/{team.Id}/month");
        Assert.Equal((700m, 2), (month.Total, month.Count));
        Assert.Equal(["Ravi 500", "Dadi 200"], month.ByName.Select(n => $"{n.Name} {n.Total:0}"));

        // The owner's view link shows totals by name to anyone with it, and stops when turned off. Members can't make one.
        Assert.Equal(HttpStatusCode.Forbidden, (await sunita.PostJsonAsync($"/api/v1/teams/{team.Id}/view-link?on=true", new { })).StatusCode);
        var linked = await (await ravi.PostJsonAsync($"/api/v1/teams/{team.Id}/view-link?on=true", new { })).ReadAsync<TeamDto>();
        Assert.True((await sunita.GetJsonAsync<TeamDto>($"/api/v1/teams/{team.Id}")).ViewLinkOn);
        Assert.Null((await sunita.GetJsonAsync<TeamDto>($"/api/v1/teams/{team.Id}")).ViewToken);
        var anyone = api.CreateClient();
        var view = await anyone.GetJsonAsync<TeamPublicView>($"/api/v1/team-view/{team.Id}?t={linked.ViewToken}");
        Assert.Equal((700m, "Ravi"), (view.MonthTotal, view.MonthByName[0].Name));
        Assert.Equal(HttpStatusCode.NotFound, (await anyone.GetAsync($"/api/v1/team-view/{team.Id}?t=00")).StatusCode);
        await ravi.PostJsonAsync($"/api/v1/teams/{team.Id}/view-link?on=false", new { });
        Assert.Equal(HttpStatusCode.NotFound, (await anyone.GetAsync($"/api/v1/team-view/{team.Id}?t={linked.ViewToken}")).StatusCode);

        // Someone outside the team sees nothing.
        var (stranger, _) = await api.SignUpAsync("Stranger shop");
        Assert.Equal(HttpStatusCode.NotFound, (await stranger.GetAsync($"/api/v1/teams/{team.Id}/month")).StatusCode);
    }

    [Fact]
    public async Task Leaving_stops_sharing_and_can_delete_your_lines_and_a_free_team_has_four_people()
    {
        var (owner, _) = await api.SignUpAsync("Owner shop");
        var team = await (await owner.PostJsonAsync("/api/v1/teams", new { name = "Trip to Puri", consent = true })).ReadAsync<TeamDto>();
        var code = (await (await owner.PostJsonAsync($"/api/v1/teams/{team.Id}/invite", new { })).ReadAsync<TeamDto>()).InviteCode;

        var (a, _) = await api.SignUpAsync("A shop");
        var (b, _) = await api.SignUpAsync("B shop");
        await a.PostJsonAsync("/api/v1/teams/join", new { code, yourName = "Amit", consent = true });
        await b.PostJsonAsync("/api/v1/teams/join", new { code, yourName = "Bina", consent = true });
        await a.PostJsonAsync($"/api/v1/teams/{team.Id}/expenses", new { id = Guid.CreateVersion7(), amount = 400m, note = "train" });
        await b.PostJsonAsync($"/api/v1/teams/{team.Id}/expenses", new { id = Guid.CreateVersion7(), amount = 250m, note = "khana" });

        // Amit leaves keeping his lines (they become "Former member"); Bina leaves and deletes hers.
        Assert.Equal(HttpStatusCode.NoContent, (await a.PostJsonAsync($"/api/v1/teams/{team.Id}/leave", new { deleteMyLines = false })).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await b.PostJsonAsync($"/api/v1/teams/{team.Id}/leave", new { deleteMyLines = true })).StatusCode);
        var month = await owner.GetJsonAsync<TeamMonthDto>($"/api/v1/teams/{team.Id}/month");
        Assert.Equal(["Former member 400"], month.ByName.Select(n => $"{n.Name} {n.Total:0}"));
        Assert.Equal(HttpStatusCode.NotFound, (await a.GetAsync($"/api/v1/teams/{team.Id}/month")).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await owner.PostJsonAsync($"/api/v1/teams/{team.Id}/leave", new { deleteMyLines = false })).StatusCode);

        // Four people at most on a free team (named members count).
        foreach (var name in new[] { "Chhotu", "Dev", "Esha" })
        {
            await owner.PostJsonAsync($"/api/v1/teams/{team.Id}/members", new { name });
        }

        Assert.Equal(HttpStatusCode.PaymentRequired, (await owner.PostJsonAsync($"/api/v1/teams/{team.Id}/members", new { name = "Farah" })).StatusCode);
    }

    [Fact]
    public async Task Big_lines_and_lines_past_a_monthly_limit_wait_for_the_owners_ok_and_can_carry_a_bill_photo()
    {
        var (owner, _) = await api.SignUpAsync("Owner shop");
        var team = await (await owner.PostJsonAsync("/api/v1/teams", new { name = "Shop staff", consent = true })).ReadAsync<TeamDto>();
        var code = (await (await owner.PostJsonAsync($"/api/v1/teams/{team.Id}/invite", new { })).ReadAsync<TeamDto>()).InviteCode;
        var (raju, _) = await api.SignUpAsync("Raju shop");
        var joined = await (await raju.PostJsonAsync("/api/v1/teams/join", new { code, yourName = "Raju", consent = true })).ReadAsync<TeamDto>();

        // Approval above ₹500; Raju may spend ₹1,000 a month.
        await owner.PutAsJsonAsync($"/api/v1/teams/{team.Id}/rules", new { approvalAbove = 500m });
        Assert.Equal(HttpStatusCode.Forbidden, (await raju.PutAsJsonAsync($"/api/v1/teams/{team.Id}/rules", new { approvalAbove = 1m })).StatusCode);
        await owner.PutAsJsonAsync($"/api/v1/teams/{team.Id}/members/{joined.MyMemberId}/limit", new { monthlyLimit = 1000m });

        const string photo = "data:image/webp;base64,UklGRhoAAABXRUJQVlA4TA0AAAAvAAAAEAcQERGIiP4HAA==";
        var small = await (await raju.PostJsonAsync($"/api/v1/teams/{team.Id}/expenses", new { id = Guid.CreateVersion7(), amount = 300m, note = "tempo", photoDataUrl = photo })).ReadAsync<TeamLineDto>();
        Assert.False(small.Pending);
        Assert.True(small.HasPhoto);
        var big = await (await raju.PostJsonAsync($"/api/v1/teams/{team.Id}/expenses", new { id = Guid.CreateVersion7(), amount = 600m, note = "repair" })).ReadAsync<TeamLineDto>();
        Assert.True(big.Pending);
        var pastLimit = await (await raju.PostJsonAsync($"/api/v1/teams/{team.Id}/expenses", new { id = Guid.CreateVersion7(), amount = 450m, note = "maal" })).ReadAsync<TeamLineDto>();
        Assert.True(pastLimit.Pending); // 300 + 600 + 450 > 1,000

        // Waiting lines don't count yet; the owner sees them, approves one and strikes the other.
        var day = await owner.GetJsonAsync<TeamDayDto>($"/api/v1/teams/{team.Id}/day");
        Assert.Equal((300m, 1050m), (day.Total, day.PendingTotal));
        Assert.Equal(2, (await owner.GetJsonAsync<List<TeamLineDto>>($"/api/v1/teams/{team.Id}/pending")).Count);
        Assert.Equal(HttpStatusCode.Forbidden, (await raju.PostJsonAsync($"/api/v1/teams/{team.Id}/expenses/{big.Id}/approve", new { })).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await owner.PostJsonAsync($"/api/v1/teams/{team.Id}/expenses/{big.Id}/approve", new { })).StatusCode);
        await owner.PostJsonAsync($"/api/v1/teams/{team.Id}/expenses/{pastLimit.Id}/strike", new { reason = "Over limit, not needed" });

        var month = await raju.GetJsonAsync<TeamMonthDto>($"/api/v1/teams/{team.Id}/month");
        Assert.Equal(900m, month.Total);
        var me = (await raju.GetJsonAsync<TeamDto>($"/api/v1/teams/{team.Id}")).Members.Single(m => m.IsMe);
        Assert.Equal((1000m, 900m), (me.MonthlyLimit, me.SpentThisMonth));

        var image = await raju.GetAsync($"/api/v1/teams/{team.Id}/expenses/{small.Id}/photo");
        Assert.Equal("image/webp", image.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task An_advance_goes_down_as_the_helper_spends_from_it_and_returns_the_rest()
    {
        var (owner, _) = await api.SignUpAsync("Owner shop");
        var team = await (await owner.PostJsonAsync("/api/v1/teams", new { name = "Site team", consent = true })).ReadAsync<TeamDto>();
        var code = (await (await owner.PostJsonAsync($"/api/v1/teams/{team.Id}/invite", new { })).ReadAsync<TeamDto>()).InviteCode;
        var (raju, _) = await api.SignUpAsync("Raju shop");
        var rajuTeam = await (await raju.PostJsonAsync("/api/v1/teams/join", new { code, yourName = "Raju", consent = true })).ReadAsync<TeamDto>();

        Assert.Equal(HttpStatusCode.Forbidden, (await raju.PostJsonAsync($"/api/v1/teams/{team.Id}/advances", new { memberId = rajuTeam.MyMemberId, kind = "given", amount = 2000m })).StatusCode);
        await owner.PostJsonAsync($"/api/v1/teams/{team.Id}/advances", new { memberId = rajuTeam.MyMemberId, kind = "given", amount = 2000m, note = "for the week" });
        await raju.PostJsonAsync($"/api/v1/teams/{team.Id}/expenses", new { id = Guid.CreateVersion7(), amount = 450m, note = "tempo", fromAdvance = true });
        await raju.PostJsonAsync($"/api/v1/teams/{team.Id}/expenses", new { id = Guid.CreateVersion7(), amount = 120m, note = "chai", fromAdvance = true });
        await raju.PostJsonAsync($"/api/v1/teams/{team.Id}/expenses", new { id = Guid.CreateVersion7(), amount = 99m, note = "own pocket" });

        var mine = await raju.GetJsonAsync<AdvancesDto>($"/api/v1/teams/{team.Id}/advances");
        Assert.Equal((2000m, 570m, 0m, 1430m), (mine.Balances[0].Given, mine.Balances[0].Spent, mine.Balances[0].Returned, mine.Balances[0].Holds));

        // He hands back ₹1,430: holds nothing.
        var settled = await (await owner.PostJsonAsync($"/api/v1/teams/{team.Id}/advances", new { memberId = rajuTeam.MyMemberId, kind = "returned", amount = 1430m })).ReadAsync<AdvancesDto>();
        Assert.Equal(0m, settled.Balances.Single(b => b.MemberId == rajuTeam.MyMemberId).Holds);
        Assert.Equal(2, settled.Entries.Count);
    }
}
