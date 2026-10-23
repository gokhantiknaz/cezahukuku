using CezaHukuku.Api.Auth;
using CezaHukuku.Api.Common;
using CezaHukuku.Api.Library;
using CezaHukuku.Domain.Laws;
using CezaHukuku.Infrastructure.Persistence;
using CezaHukuku.Parsing.Text;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;

namespace CezaHukuku.Api.Admin;

/// <summary>Kütüphane yönetimi: kanun grupları ve kanunlar. Ağaç ve maddeler ayrı dosyalarda.</summary>
public static class AdminLawEndpoints
{
    // Kanun adresleri kökte (/tck/madde-81) olduğundan web uygulamasının kendi sayfalarıyla çakışan adlar alınamaz.
    private static readonly HashSet<string> ReservedLawSlugs =
        ["api", "ara", "giris", "cikis", "panel", "hesap", "kanunlar", "hakkinda", "favoriler", "notlarim"];

    public static RouteGroupBuilder MapAdminLawEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/admin")
            .WithTags("AdminLaws")
            .RequireAuthorization(Policies.AdminOnly);

        group.MapPost("/law-groups", CreateGroupAsync).WithName("CreateLawGroup");
        group.MapPut("/law-groups/{id:guid}", UpdateGroupAsync).WithName("UpdateLawGroup");
        group.MapDelete("/law-groups/{id:guid}", DeleteGroupAsync).WithName("DeleteLawGroup");

        group.MapPost("/laws", CreateLawAsync).WithName("CreateLaw");
        group.MapPut("/laws/{id:guid}", UpdateLawAsync).WithName("UpdateLaw");
        group.MapDelete("/laws/{id:guid}", DeleteLawAsync).WithName("DeleteLaw");

        group.MapLawNodeEndpoints();
        group.MapArticleEndpoints();
        group.MapImportEndpoints();
        group.MapNoteEndpoints();
        group.MapDecisionEndpoints();

        return group;
    }

    private static async Task<Results<Created<LawGroupSummary>, ValidationProblem>> CreateGroupAsync(
        SaveLawGroupRequest request, AppDbContext db)
    {
        var group = new LawGroup { Slug = "", Title = "" };
        if (await ApplyAsync(group, request, db) is { } problem)
            return problem;

        group.SortOrder = request.SortOrder ?? (await db.LawGroups.MaxAsync(g => (int?)g.SortOrder) ?? 0) + 1;
        db.LawGroups.Add(group);
        await db.SaveChangesAsync();
        return TypedResults.Created($"/api/admin/law-groups/{group.Id}", group.ToSummary());
    }

    private static async Task<Results<Ok<LawGroupSummary>, NotFound, ValidationProblem>> UpdateGroupAsync(
        Guid id, SaveLawGroupRequest request, AppDbContext db)
    {
        var group = await db.LawGroups.FindAsync(id);
        if (group is null)
            return TypedResults.NotFound();
        if (await ApplyAsync(group, request, db) is { } problem)
            return problem;

        group.SortOrder = request.SortOrder ?? group.SortOrder;
        await db.SaveChangesAsync();
        return TypedResults.Ok(group.ToSummary());
    }

    private static async Task<Results<NoContent, NotFound, ValidationProblem>> DeleteGroupAsync(
        Guid id, AppDbContext db, TimeProvider clock)
    {
        var group = await db.LawGroups.FindAsync(id);
        if (group is null)
            return TypedResults.NotFound();
        if (await db.Laws.AnyAsync(l => l.GroupId == id))
            return ValidationErrors.Single("group", "Grupta kanun var; önce kanunları başka gruba taşıyın veya silin.");

        group.DeletedAt = clock.GetUtcNow();
        await db.SaveChangesAsync();
        return TypedResults.NoContent();
    }

    private static async Task<ValidationProblem?> ApplyAsync(LawGroup group, SaveLawGroupRequest request, AppDbContext db)
    {
        var errors = new ValidationErrors().Required("title", request.Title, 200);
        var title = InputText.CleanRequired(request.Title);
        var slug = InputText.Clean(request.Slug)?.ToLowerInvariant() ?? Slugs.FromText(title);

        if (!errors.HasErrors)
        {
            if (!Slugs.IsValid(slug) || slug.Length > 100)
                errors.Add("slug", "Yalnız küçük harf, rakam ve tire kullanılabilir (en fazla 100 karakter).");
            else if (await db.LawGroups.AnyAsync(g => g.Slug == slug && g.Id != group.Id))
                errors.Add("slug", "Bu kısa ad başka bir grupta kullanılıyor.");
        }
        if (errors.HasErrors)
            return errors.ToProblem();

        group.Title = title;
        group.Slug = slug;
        return null;
    }

    private static async Task<Results<Created<LawSummary>, ValidationProblem>> CreateLawAsync(
        SaveLawRequest request, AppDbContext db)
    {
        var law = new Law { Slug = "", Number = "", Name = "" };
        if (await ApplyAsync(law, request, db) is { } problem)
            return problem;

        law.SortOrder = request.SortOrder
            ?? (await db.Laws.Where(l => l.GroupId == law.GroupId).MaxAsync(l => (int?)l.SortOrder) ?? 0) + 1;
        db.Laws.Add(law);
        await db.SaveChangesAsync();
        return TypedResults.Created($"/api/admin/laws/{law.Id}", law.ToSummary());
    }

    private static async Task<Results<Ok<LawSummary>, NotFound, ValidationProblem>> UpdateLawAsync(
        Guid id, SaveLawRequest request, AppDbContext db)
    {
        var law = await db.Laws.FindAsync(id);
        if (law is null)
            return TypedResults.NotFound();
        if (await ApplyAsync(law, request, db) is { } problem)
            return problem;

        law.SortOrder = request.SortOrder ?? law.SortOrder;
        await db.SaveChangesAsync();
        return TypedResults.Ok(law.ToSummary());
    }

    /// <summary>Kanun silinir (soft delete); ağacı ve maddeleri kanunla birlikte erişilmez olur, kayıtları kalır.</summary>
    private static async Task<Results<NoContent, NotFound>> DeleteLawAsync(Guid id, AppDbContext db, TimeProvider clock)
    {
        var law = await db.Laws.FindAsync(id);
        if (law is null)
            return TypedResults.NotFound();

        law.DeletedAt = clock.GetUtcNow();
        await db.SaveChangesAsync();
        return TypedResults.NoContent();
    }

    private static async Task<ValidationProblem?> ApplyAsync(Law law, SaveLawRequest request, AppDbContext db)
    {
        var errors = new ValidationErrors()
            .Required("number", request.Number, 50)
            .Required("name", request.Name, 500)
            .MaxLength("shortName", request.ShortName, 50)
            .MaxLength("gazetteNumber", request.GazetteNumber, 50);

        var number = InputText.CleanRequired(request.Number);
        var shortName = InputText.Clean(request.ShortName);
        var slug = InputText.Clean(request.Slug)?.ToLowerInvariant() ?? Slugs.FromText(shortName ?? number);

        // Numara boşken türetilen kısa ad da boş olur; aynı eksik için ikinci bir hata gösterilmesin.
        if (!errors.HasErrors)
        {
            if (!Slugs.IsValid(slug) || slug.Length > 100)
                errors.Add("slug", "Yalnız küçük harf, rakam ve tire kullanılabilir (en fazla 100 karakter).");
            else if (ReservedLawSlugs.Contains(slug))
                errors.Add("slug", "Bu kısa ad sitenin bir sayfasıyla çakışıyor; başka bir ad seçin.");
            else if (await db.Laws.AnyAsync(l => l.Slug == slug && l.Id != law.Id))
                errors.Add("slug", "Bu kısa ad başka bir kanunda kullanılıyor.");
        }

        if (!await db.LawGroups.AnyAsync(g => g.Id == request.GroupId))
            errors.Add("groupId", "Kanun grubu bulunamadı.");

        if (errors.HasErrors)
            return errors.ToProblem();

        law.GroupId = request.GroupId;
        law.Number = number;
        law.Name = InputText.CleanRequired(request.Name);
        law.ShortName = shortName;
        law.Slug = slug;
        law.AcceptedOn = request.AcceptedOn;
        law.GazetteDate = request.GazetteDate;
        law.GazetteNumber = InputText.Clean(request.GazetteNumber);
        law.Visibility = request.Visibility;
        return null;
    }
}
