using CezaHukuku.Api.Common;
using CezaHukuku.Api.Library;
using CezaHukuku.Domain.Laws;
using CezaHukuku.Infrastructure.Persistence;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;

namespace CezaHukuku.Api.Admin;

/// <summary>Kanun ağacı düğümleri (Kitap/Kısım/Bölüm/Ayrım).</summary>
internal static class AdminLawNodeEndpoints
{
    public static void MapLawNodeEndpoints(this RouteGroupBuilder group)
    {
        group.MapPost("/laws/{lawId:guid}/nodes", CreateAsync).WithName("CreateLawNode");
        group.MapPut("/nodes/{id:guid}", UpdateAsync).WithName("UpdateLawNode");
        group.MapDelete("/nodes/{id:guid}", DeleteAsync).WithName("DeleteLawNode");
    }

    private static async Task<Results<Created<LawNodeSummary>, NotFound, ValidationProblem>> CreateAsync(
        Guid lawId, SaveLawNodeRequest request, AppDbContext db)
    {
        if (!await db.Laws.AnyAsync(l => l.Id == lawId))
            return TypedResults.NotFound();

        var node = new LawNode { LawId = lawId, Number = "", Title = "" };
        if (await ApplyAsync(node, request, db) is { } problem)
            return problem;

        node.SortOrder = request.SortOrder ?? await NextSortOrderAsync(db, lawId, node.ParentId);
        db.LawNodes.Add(node);
        await db.SaveChangesAsync();
        return TypedResults.Created($"/api/admin/nodes/{node.Id}", node.ToSummary());
    }

    private static async Task<Results<Ok<LawNodeSummary>, NotFound, ValidationProblem>> UpdateAsync(
        Guid id, SaveLawNodeRequest request, AppDbContext db)
    {
        var node = await db.LawNodes.FindAsync(id);
        if (node is null)
            return TypedResults.NotFound();

        var previousParent = node.ParentId;
        if (await ApplyAsync(node, request, db) is { } problem)
            return problem;

        // Başka bir üst düğüme taşınıp sıra verilmediyse yeni yerin sonuna eklenir.
        node.SortOrder = request.SortOrder
            ?? (node.ParentId == previousParent ? node.SortOrder : await NextSortOrderAsync(db, node.LawId, node.ParentId));
        await db.SaveChangesAsync();
        return TypedResults.Ok(node.ToSummary());
    }

    /// <summary>Yalnız boş düğüm silinir; içindeki maddeler ve alt düğümler yanlışlıkla kaybolmasın.</summary>
    private static async Task<Results<NoContent, NotFound, ValidationProblem>> DeleteAsync(
        Guid id, AppDbContext db, TimeProvider clock)
    {
        var node = await db.LawNodes.FindAsync(id);
        if (node is null)
            return TypedResults.NotFound();
        if (await db.LawNodes.AnyAsync(n => n.ParentId == id) || await db.Articles.AnyAsync(a => a.NodeId == id))
            return ValidationErrors.Single("node", "Düğüm boş değil; önce alt düğümleri ve maddeleri taşıyın veya silin.");

        node.DeletedAt = clock.GetUtcNow();
        await db.SaveChangesAsync();
        return TypedResults.NoContent();
    }

    private static async Task<ValidationProblem?> ApplyAsync(LawNode node, SaveLawNodeRequest request, AppDbContext db)
    {
        var errors = new ValidationErrors()
            .Required("number", request.Number, 100)
            .Required("title", request.Title, 500);

        if (request.ParentId is { } parentId)
        {
            // Üst düğüm aynı kanunda olmalı ve düğüm kendi altına taşınamaz (döngü).
            var parents = await db.LawNodes.Where(n => n.LawId == node.LawId)
                .ToDictionaryAsync(n => n.Id, n => n.ParentId);
            if (!parents.ContainsKey(parentId))
                errors.Add("parentId", "Üst düğüm bu kanunda bulunamadı.");
            else if (IsSelfOrDescendant(parentId, node.Id, parents))
                errors.Add("parentId", "Düğüm kendisinin ya da alt düğümünün altına taşınamaz.");
        }

        if (errors.HasErrors)
            return errors.ToProblem();

        node.ParentId = request.ParentId;
        node.Kind = request.Kind;
        node.Number = InputText.CleanRequired(request.Number);
        node.Title = InputText.CleanRequired(request.Title);
        node.Visibility = request.Visibility;
        return null;
    }

    /// <summary><paramref name="candidate"/> düğümü <paramref name="nodeId"/>'nin kendisi ya da alt düğümü mü?</summary>
    private static bool IsSelfOrDescendant(Guid candidate, Guid nodeId, Dictionary<Guid, Guid?> parents)
    {
        var seen = new HashSet<Guid>();
        for (Guid? current = candidate; current is { } id && seen.Add(id); current = parents.GetValueOrDefault(id))
        {
            if (id == nodeId)
                return true;
        }
        return false;
    }

    /// <summary>Aynı üst öğedeki düğüm ve maddeler tek sıra uzayını paylaşır; yeni öğe en sona eklenir.</summary>
    internal static async Task<int> NextSortOrderAsync(AppDbContext db, Guid lawId, Guid? parentNodeId)
    {
        var maxNode = await db.LawNodes.Where(n => n.LawId == lawId && n.ParentId == parentNodeId)
            .MaxAsync(n => (int?)n.SortOrder);
        var maxArticle = await db.Articles.Where(a => a.LawId == lawId && a.NodeId == parentNodeId)
            .MaxAsync(a => (int?)a.SortOrder);
        return Math.Max(maxNode ?? 0, maxArticle ?? 0) + 1;
    }
}
