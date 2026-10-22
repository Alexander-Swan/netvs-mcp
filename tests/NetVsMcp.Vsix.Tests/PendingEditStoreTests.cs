using System;
using System.Threading;
using NetVsMcp.Vsix;

namespace NetVsMcp.Vsix.Tests;

public class PendingEditStoreTests
{
    [Theory]
    [InlineData("write")]
    [InlineData("insert")]
    [InlineData("replace")]
    public void Approve_RejectsChangesOutsideThePreviewRange_AndRetainsEdit(string operation)
    {
        var store = CreateStore(operation);
        var called = false;

        var result = store.Approve("edit-1",
            _ => new EditDocumentSnapshot(true, "// new line\nclass Example {}\n"),
            _ => { called = true; return Mutation(true); });

        Assert.False(result.Success);
        Assert.False(result.Applied);
        Assert.Contains("changed since", result.Message);
        Assert.False(called);
        Assert.Single(store.List());
        Assert.True(store.Reject("edit-1").Success);
        Assert.Empty(store.List());
    }

    [Theory]
    [InlineData(false, true)]
    [InlineData(true, false)]
    public void Approve_RejectsFileCreationOrDeletion_EvenWhenTextIsEmpty(bool existed, bool exists)
    {
        var store = CreateStore("write", new EditDocumentSnapshot(existed, ""));

        var result = store.Approve("edit-1", _ => new EditDocumentSnapshot(exists, ""),
            _ => throw new Exception("Must not mutate a changed document."));

        Assert.False(result.Success);
        Assert.Single(store.List());
    }

    [Fact]
    public void Approve_UsesExactTextIncludingCaseAndLineEndings()
    {
        foreach (var changed in new[] { "CLASS Example {}\n", "class Example {}\r\n" })
        {
            var store = CreateStore();
            var result = store.Approve("edit-1", _ => new EditDocumentSnapshot(true, changed),
                _ => throw new Exception("Must not mutate a changed document."));
            Assert.False(result.Success);
            Assert.Single(store.List());
        }
    }

    [Fact]
    public void Approve_SuccessConsumesEdit_AndCannotApplyTwice()
    {
        var store = CreateStore();
        var applications = 0;
        DocumentMutationResult Apply(PendingEdit _) { applications++; return Mutation(true); }

        var result = store.Approve("edit-1", edit => edit.Snapshot, Apply);
        var repeat = store.Approve("edit-1", edit => edit.Snapshot, Apply);

        Assert.True(result.Success);
        Assert.True(result.Applied);
        Assert.False(repeat.Success);
        Assert.Equal(1, applications);
        Assert.Empty(store.List());
    }

    [Fact]
    public void Approve_FailedMutationCanBeRetried()
    {
        var store = CreateStore();
        var failure = store.Approve("edit-1", edit => edit.Snapshot, _ => Mutation(false));

        Assert.False(failure.Success);
        Assert.Single(store.List());
        Assert.True(store.Approve("edit-1", edit => edit.Snapshot, _ => Mutation(true)).Success);
        Assert.Empty(store.List());
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Approve_ExceptionsRetainEdit_AndReleaseApplyGuard(bool failReading)
    {
        var store = CreateStore();
        var result = store.Approve("edit-1",
            edit => failReading ? throw new InvalidOperationException("read failed") : edit.Snapshot,
            _ => throw new InvalidOperationException("apply failed"));

        Assert.False(result.Success);
        Assert.Single(store.List());
        Assert.True(store.Approve("edit-1", edit => edit.Snapshot, _ => Mutation(true)).Success);
    }

    [Fact]
    public void Approve_CancellationRetainsEdit()
    {
        var store = CreateStore();
        Assert.Throws<OperationCanceledException>(() => store.Approve("edit-1",
            edit => edit.Snapshot, _ => throw new OperationCanceledException()));

        Assert.Single(store.List());
        Assert.True(store.Reject("edit-1").Success);
    }

    [Fact]
    public void Approve_PartialMutationCannotBeRetriedAgainstChangedBuffer()
    {
        var store = CreateStore();
        var current = new EditDocumentSnapshot(true, "class Example {}\n");
        var first = store.Approve("edit-1", _ => current, _ =>
        {
            current = new EditDocumentSnapshot(true, "partially applied text");
            throw new InvalidOperationException("Save failed after changing buffer.");
        });
        var retry = store.Approve("edit-1", _ => current,
            _ => throw new Exception("Must not apply again after a partial mutation."));

        Assert.False(first.Success);
        Assert.False(retry.Success);
        Assert.Contains("changed since", retry.Message);
        Assert.Single(store.List());
    }

    [Fact]
    public async System.Threading.Tasks.Task Approve_ConcurrentApprovalAndRejectionCannotConsumeAnApplyingEdit()
    {
        var store = CreateStore();
        using var entered = new ManualResetEventSlim();
        using var release = new ManualResetEventSlim();
        var task = System.Threading.Tasks.Task.Run(() => store.Approve("edit-1", edit => edit.Snapshot, _ =>
        {
            entered.Set();
            if (!release.Wait(TimeSpan.FromSeconds(10))) throw new TimeoutException();
            return Mutation(true);
        }));

        try
        {
            Assert.True(entered.Wait(TimeSpan.FromSeconds(10)));
            Assert.False(store.Reject("edit-1").Success);
            Assert.False(store.Approve("edit-1", edit => edit.Snapshot,
                _ => throw new Exception("Must not apply concurrently.")).Success);
            Assert.Single(store.List());
        }
        finally { release.Set(); }

        Assert.True((await task).Success);
        Assert.Empty(store.List());
    }

    private static PendingEditStore CreateStore(string operation = "write", EditDocumentSnapshot? snapshot = null)
    {
        var store = new PendingEditStore();
        var info = new PendingEditInfo("edit-1", operation, "Example.cs", "Edit", "", "replacement",
            1, 1, 1, 6, 0, 11, DateTimeOffset.UtcNow);
        store.Add(new PendingEdit(info, new EditPreviewRequest { Path = "Example.cs", Operation = operation },
            snapshot ?? new EditDocumentSnapshot(true, "class Example {}\n")));
        return store;
    }

    private static DocumentMutationResult Mutation(bool success) =>
        new(success, success ? null : "Mutation failed.", null, false, 0);
}
