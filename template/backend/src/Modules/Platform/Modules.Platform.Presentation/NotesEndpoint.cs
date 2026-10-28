using System.Security.Claims;
using Common.Presentation;
using ErrorOr;
using Immediate.Handlers.Shared;
using JetBrains.Annotations;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Modules.Platform.Application.Notes;

namespace Modules.Platform.Presentation;

/// <summary>The body of POST /v1/platform/notes.</summary>
[UsedImplicitly(ImplicitUseTargetFlags.WithMembers)] // read by the JSON serializer
public sealed record CreateNoteRequest(string Text);

/// <summary>
/// POST /v1/platform/notes and GET /v1/platform/notes/{id}: the example stored resource every new
/// one copies. Both need a signed-in user; notes belong to the user who wrote them.
/// </summary>
internal static class NotesEndpoint
{
    internal static void MapNotes(this IEndpointRouteBuilder app)
    {
        app.MapPost("v1/platform/notes", async (
                CreateNoteRequest request,
                ClaimsPrincipal user,
                IHandler<CreateNoteCommand, ErrorOr<NoteResponse>> handler,
                CancellationToken cancellationToken) =>
                ApiResults.Created(
                    await handler.HandleAsync(new CreateNoteCommand(OwnerOf(user), request.Text), cancellationToken),
                    note => $"/v1/platform/notes/{note.Id}"))
            .RequirePermission(PlatformPermissions.WriteNotes)
            .WithName("CreateNote")
            .WithTags("Notes")
            .Produces<NoteResponse>(StatusCodes.Status201Created)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden);

        app.MapGet("v1/platform/notes/{id:guid}", async (
                Guid id,
                ClaimsPrincipal user,
                IHandler<GetNoteQuery, ErrorOr<NoteResponse>> handler,
                CancellationToken cancellationToken) =>
                ApiResults.Ok(await handler.HandleAsync(new GetNoteQuery(OwnerOf(user), id), cancellationToken)))
            .RequirePermission(PlatformPermissions.ReadNotes)
            .WithName("GetNote")
            .WithTags("Notes")
            .Produces<NoteResponse>()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);
    }

    private static string OwnerOf(ClaimsPrincipal user) => user.FindFirstValue(SignedInUser.IdClaim)!;
}
