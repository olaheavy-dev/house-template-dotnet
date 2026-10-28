using Common.Application.Paging;

namespace Common.UnitTests;

public sealed class PagingTests
{
    [Test]
    public void A_cursor_round_trips_its_key()
    {
        var id = Guid.CreateVersion7();

        Assert.That(Cursor.Decode<Guid>(Cursor.Encode(id)).Value, Is.EqualTo(id));
        Assert.That(Cursor.Decode<long>(Cursor.Encode(42L)).Value, Is.EqualTo(42L));
    }

    [Test]
    public void No_cursor_means_the_first_page()
    {
        var result = Cursor.Decode<Guid>(null);

        Assert.That(result.IsError, Is.False);
        Assert.That(result.Value, Is.Null);
    }

    [TestCase("not base64!")]
    [TestCase("")]
    [TestCase("bm90IGpzb24")]
    public void A_damaged_cursor_is_a_validation_error(string cursor)
    {
        var result = Cursor.Decode<Guid>(cursor);

        Assert.That(result.IsError, Is.True);
        Assert.That(result.FirstError.Code, Is.EqualTo("cursor"));
    }

    [Test]
    public void A_cursor_for_another_key_type_is_refused()
    {
        Assert.That(Cursor.Decode<Guid>(Cursor.Encode("a string, not an id")).IsError, Is.True);
        Assert.That(Cursor.Decode<Guid>(Cursor.Encode(7)).IsError, Is.True);
        Assert.That(Cursor.Decode<long>(Cursor.Encode(true)).IsError, Is.True);
    }

    [Test]
    public void A_page_takes_the_limit_and_points_at_the_last_row_shown_when_more_remain()
    {
        var page = Page.From([1, 2, 3], new PageRequest(2), row => row, row => row * 10);

        Assert.That(page.Items, Is.EqualTo(new[] { 10, 20 }));
        Assert.That(Cursor.Decode<int>(page.NextCursor).Value, Is.EqualTo(2));
    }

    [Test]
    public void A_page_has_no_cursor_when_nothing_remains()
    {
        Assert.That(Page.From([1, 2], new PageRequest(2), row => row, row => row).NextCursor, Is.Null);
        Assert.That(Page.From(Array.Empty<int>(), new PageRequest(2), row => row, row => row).NextCursor, Is.Null);
    }

    [TestCase(0, false)]
    [TestCase(1, true)]
    [TestCase(100, true)]
    [TestCase(101, false)]
    public void The_limit_is_1_to_100(int limit, bool valid) =>
        Assert.That(new PageRequestValidator().Validate(new PageRequest(limit)).IsValid, Is.EqualTo(valid));

    [Test]
    public void A_cursor_over_512_characters_is_refused() =>
        Assert.That(new PageRequestValidator().Validate(new PageRequest(20, new string('a', 513))).IsValid, Is.False);
}
