using GroceryKhata.Api.Identity;

namespace GroceryKhata.Api.Tests;

public class SlugTests
{
    [Fact]
    public void A_shop_link_is_its_name_and_an_eight_character_code()
    {
        var slug = AuthService.NewSlug("Sharma General Store");
        Assert.Matches("^sharma-general-store-[0-9a-f]{8}$", slug);
        Assert.Matches("^shop-[0-9a-f]{8}$", AuthService.NewSlug("॥ ॥"));
    }

    [Fact]
    public void Many_shops_with_the_same_name_get_different_links()
    {
        // With four characters, 2,000 shops of one name clashed almost every time; sign-up then failed.
        var slugs = Enumerable.Range(0, 20_000).Select(_ => AuthService.NewSlug("Jai Mata Di Kirana")).ToList();
        Assert.Equal(slugs.Count, slugs.Distinct().Count());
    }
}
