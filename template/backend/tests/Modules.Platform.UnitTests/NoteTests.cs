using Modules.Platform.Domain;

namespace Modules.Platform.UnitTests;

public sealed class NoteTests
{
    private static readonly Guid Id = Guid.Parse("018f0000-0000-7000-8000-000000000001");

    [Test]
    public void Create_trims_the_text_and_keeps_the_owner()
    {
        var result = Note.Create(Id, "alice", "  buy milk  ");

        Assert.That(result.IsError, Is.False);
        Assert.That(result.Value.Text, Is.EqualTo("buy milk"));
        Assert.That(result.Value.OwnerId, Is.EqualTo("alice"));
    }

    [TestCase("")]
    [TestCase("   ")]
    public void Create_refuses_a_blank_text(string text)
    {
        var result = Note.Create(Id, "alice", text);

        Assert.That(result.IsError, Is.True);
        Assert.That(result.FirstError.Code, Is.EqualTo("note.text"));
    }

    [Test]
    public void Create_refuses_a_text_over_the_limit()
    {
        Assert.That(Note.Create(Id, "alice", new string('a', Note.MaxLength)).IsError, Is.False);
        Assert.That(Note.Create(Id, "alice", new string('a', Note.MaxLength + 1)).IsError, Is.True);
    }

    [Test]
    public void Edit_replaces_the_text_or_refuses_it()
    {
        var note = Note.Create(Id, "alice", "one").Value;

        Assert.That(note.Edit("two").IsError, Is.False);
        Assert.That(note.Text, Is.EqualTo("two"));
        Assert.That(note.Edit(" ").IsError, Is.True);
        Assert.That(note.Text, Is.EqualTo("two"));
    }
}
