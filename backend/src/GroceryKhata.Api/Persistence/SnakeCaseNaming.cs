using System.Text;
using Microsoft.EntityFrameworkCore;

namespace GroceryKhata.Api.Persistence;

/// <summary>Turns C# names into PostgreSQL snake_case (ShopId → shop_id).</summary>
public static class SnakeCaseNaming
{
    /// <summary>Renames every column to snake_case. Table names are set in the model.</summary>
    public static void Apply(ModelBuilder modelBuilder)
    {
        foreach (var entity in modelBuilder.Model.GetEntityTypes())
        {
            foreach (var property in entity.GetProperties())
            {
                if (property.GetColumnName() == property.Name)
                {
                    property.SetColumnName(ToSnakeCase(property.Name));
                }
            }
        }
    }

    public static string ToSnakeCase(string name)
    {
        var builder = new StringBuilder(name.Length + 8);
        for (var i = 0; i < name.Length; i++)
        {
            var c = name[i];
            if (char.IsUpper(c))
            {
                var previousIsLowerOrDigit = i > 0 && (char.IsLower(name[i - 1]) || char.IsDigit(name[i - 1]));
                var startsWordAfterAcronym = i > 0 && char.IsUpper(name[i - 1]) && i + 1 < name.Length && char.IsLower(name[i + 1]);
                if (previousIsLowerOrDigit || startsWordAfterAcronym)
                {
                    builder.Append('_');
                }

                builder.Append(char.ToLowerInvariant(c));
            }
            else
            {
                builder.Append(c);
            }
        }

        return builder.ToString();
    }
}
