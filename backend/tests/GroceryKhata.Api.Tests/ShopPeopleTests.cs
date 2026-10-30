using System.Net;
using GroceryKhata.Api.Identity;
using GroceryKhata.Api.Shops;

namespace GroceryKhata.Api.Tests;

/// <summary>Owners and helper logins per plan: opening a shop with several people, the plan's limits, the last owner.</summary>
[Collection(ApiCollection.Name)]
public sealed class ShopPeopleTests(ApiFactory api) : AgentTestBase(api)
{
    [Fact]
    public async Task An_admin_opens_a_shop_with_two_owners_and_a_helper_within_the_plans_limits()
    {
        var admin = await AdminAsync("Approver");
        var created = await (await admin.PostJsonAsync("/api/v1/platform/shops", new
        {
            shopName = "Agarwal Traders", ownerName = "Mohan Agarwal", phone = ApiFactory.NewPhone(), type = "kirana",
            people = new object[]
            {
                new { name = "Sita Agarwal", phone = ApiFactory.NewPhone(), role = "owner" },
                new { name = "Raju", phone = ApiFactory.NewPhone(), role = "staff", pin = "4826" },
            },
        })).ReadAsync<CreatedShop>();

        // The trial is Gold: 3 owners. A third fits, a fourth doesn't.
        Assert.Equal(HttpStatusCode.Created, (await admin.PostJsonAsync($"/api/v1/platform/shops/{created.ShopId}/people", new { name = "Third Owner", phone = ApiFactory.NewPhone(), role = "owner" })).StatusCode);
        var fourth = await admin.PostJsonAsync($"/api/v1/platform/shops/{created.ShopId}/people", new { name = "Fourth Owner", phone = ApiFactory.NewPhone(), role = "owner" });
        Assert.Equal(HttpStatusCode.PaymentRequired, fourth.StatusCode);

        // A helper needs a proper PIN.
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostJsonAsync($"/api/v1/platform/shops/{created.ShopId}/people", new { name = "Helper", phone = ApiFactory.NewPhone(), role = "staff", pin = "1234" })).StatusCode);

        var detail = await admin.GetJsonAsync<PlatformShopDetail>($"/api/v1/platform/shops/{created.ShopId}");
        Assert.Equal(3, detail.People.Count(p => p.Role == ShopRole.Owner));
        Assert.Single(detail.People, p => p.Role == ShopRole.Staff);

        // Owners can be turned off, but never the last one.
        var owners = detail.People.Where(p => p.Role == ShopRole.Owner).ToList();
        foreach (var o in owners.Skip(1))
        {
            Assert.Equal(HttpStatusCode.NoContent, (await admin.PostJsonAsync($"/api/v1/platform/shops/{created.ShopId}/people/{o.MembershipId}/active?active=false", new { })).StatusCode);
        }

        Assert.Equal(HttpStatusCode.Conflict, (await admin.PostJsonAsync($"/api/v1/platform/shops/{created.ShopId}/people/{owners[0].MembershipId}/active?active=false", new { })).StatusCode);
    }

    [Fact]
    public async Task On_the_free_plan_the_owner_can_add_neither_a_second_owner_nor_a_helper()
    {
        var (owner, auth) = await Api.SignUpAsync();
        await SqlAsync("""
            INSERT INTO subscriptions (id, shop_id, trial_ends_at, referral_rewarded, created_at, updated_at)
            VALUES (gen_random_uuid(), @s, now() - interval '60 days', false, now(), now())
            ON CONFLICT (shop_id) DO UPDATE SET trial_ends_at = now() - interval '60 days'
            """, ("s", auth.ShopId!.Value));

        Assert.Equal(HttpStatusCode.PaymentRequired, (await owner.PostJsonAsync("/api/v1/shop/staff", new { name = "Raju", phone = ApiFactory.NewPhone(), pin = "4826" })).StatusCode);
        Assert.Equal(HttpStatusCode.PaymentRequired, (await owner.PostJsonAsync("/api/v1/shop/staff", new { name = "Partner", phone = ApiFactory.NewPhone(), role = "owner" })).StatusCode);
    }
}
