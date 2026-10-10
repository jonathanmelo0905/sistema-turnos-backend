using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using AgendaApp.Context;
using Microsoft.EntityFrameworkCore;

namespace AgendaApp.Services
{
    // "Barbería Demo" → "barberia-demo" (único entre empresas)
    public static class SlugHelper
    {
        public static string Normalize(string text)
        {
            var sinTildes = new string(text.Normalize(NormalizationForm.FormD)
                .Where(c => CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
                .ToArray());
            var slug = Regex.Replace(sinTildes.ToLowerInvariant(), "[^a-z0-9]+", "-").Trim('-');
            if (slug.Length > 60) slug = slug[..60].Trim('-');
            return string.IsNullOrEmpty(slug) ? "empresa" : slug;
        }

        public static async Task<string> UniqueAsync(AgendaProDbContext context, string name, Guid? exceptBusinessId = null)
        {
            var baseSlug = Normalize(name);
            var slug = baseSlug;
            var n = 2;
            while (await context.Businesses.AnyAsync(b => b.Slug == slug && b.BusinessId != exceptBusinessId))
                slug = $"{baseSlug}-{n++}";
            return slug;
        }

        // Al arrancar: las empresas sin dirección pública reciben una
        public static async Task BackfillAsync(AgendaProDbContext context)
        {
            var sinSlug = await context.Businesses.Where(b => b.Slug == null || b.Slug == "").ToListAsync();
            foreach (var b in sinSlug)
            {
                b.Slug = await UniqueAsync(context, b.Name, b.BusinessId);
                await context.SaveChangesAsync();
            }
        }
    }
}
