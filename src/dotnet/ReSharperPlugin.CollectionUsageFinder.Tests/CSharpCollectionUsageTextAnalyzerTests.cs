using System.Linq;
using NUnit.Framework;
using ReSharperPlugin.CollectionUsageFinder.Search;

namespace ReSharperPlugin.CollectionUsageFinder.Tests
{
    [TestFixture]
    public class CSharpCollectionUsageTextAnalyzerTests
    {
        private readonly CSharpCollectionUsageTextAnalyzer analyzer = new CSharpCollectionUsageTextAnalyzer();

        [Test]
        public void Analyze_FindsStructuralUsages()
        {
            var source = @"
class C
{
    void M(System.Collections.Generic.List<Item> list, Item item)
    {
        list.Add(item);
        list.RemoveAt(0);
        list.Clear();
    }
}";
            var occurrences = analyzer.Analyze(source, "list");

            Assert.That(occurrences.Select(static occurrence => occurrence.Kind), Is.EqualTo(new[]
            {
                CollectionUsageKind.CollectionStructureUsage,
                CollectionUsageKind.CollectionStructureUsage,
                CollectionUsageKind.CollectionStructureUsage
            }));
        }

        [Test]
        public void Analyze_FindsDictionaryTryAddAndListRemoveAllAsStructuralUsages()
        {
            var source = @"
class C
{
    void M(System.Collections.Generic.Dictionary<string, Item> map, System.Collections.Generic.List<Item> list, Item item)
    {
        map.TryAdd(""key"", item);
        list.RemoveAll(static item => item.Done);
    }
}";

            var dictionaryOccurrences = analyzer.Analyze(source, "map");
            var listOccurrences = analyzer.Analyze(source, "list");

            Assert.That(dictionaryOccurrences.Single().Kind, Is.EqualTo(CollectionUsageKind.CollectionStructureUsage));
            Assert.That(dictionaryOccurrences.Single().Text, Is.EqualTo(@"map.TryAdd(""key"", item);"));
            Assert.That(listOccurrences.Single().Kind, Is.EqualTo(CollectionUsageKind.CollectionStructureUsage));
            Assert.That(listOccurrences.Single().Text, Is.EqualTo("list.RemoveAll(static item => item.Done);"));
        }

        [Test]
        public void Analyze_FindsSetOperationStructuralUsages()
        {
            var source = @"
class C
{
    void M(System.Collections.Generic.HashSet<Item> set, System.Collections.Generic.IEnumerable<Item> other)
    {
        set.RemoveWhere(static item => item.Done);
        set.UnionWith(other);
        set.IntersectWith(other);
        set.ExceptWith(other);
        set.SymmetricExceptWith(other);
    }
}";
            var occurrences = analyzer.Analyze(source, "set");

            Assert.That(occurrences.Select(static occurrence => occurrence.Text), Is.EqualTo(new[]
            {
                "set.RemoveWhere(static item => item.Done);",
                "set.UnionWith(other);",
                "set.IntersectWith(other);",
                "set.ExceptWith(other);",
                "set.SymmetricExceptWith(other);"
            }));
            Assert.That(
                occurrences.Select(static occurrence => occurrence.Kind),
                Is.All.EqualTo(CollectionUsageKind.CollectionStructureUsage));
        }

        [Test]
        public void Analyze_FindsQueueAndStackStructuralUsages()
        {
            var source = @"
class C
{
    void M(System.Collections.Generic.Queue<Item> queue, System.Collections.Generic.Stack<Item> stack, Item item)
    {
        queue.Enqueue(item);
        queue.Dequeue();
        queue.TryDequeue(out var dequeued);
        stack.Push(item);
        stack.Pop();
        stack.TryPop(out var popped);
    }
}";

            var queueOccurrences = analyzer.Analyze(source, "queue");
            var stackOccurrences = analyzer.Analyze(source, "stack");

            Assert.That(queueOccurrences.Select(static occurrence => occurrence.Text), Is.EqualTo(new[]
            {
                "queue.Enqueue(item);",
                "queue.Dequeue();",
                "queue.TryDequeue(out var dequeued);"
            }));
            Assert.That(stackOccurrences.Select(static occurrence => occurrence.Text), Is.EqualTo(new[]
            {
                "stack.Push(item);",
                "stack.Pop();",
                "stack.TryPop(out var popped);"
            }));
            Assert.That(
                queueOccurrences.Concat(stackOccurrences).Select(static occurrence => occurrence.Kind),
                Is.All.EqualTo(CollectionUsageKind.CollectionStructureUsage));
        }

        [Test]
        public void Analyze_UsesTargetReferenceFilter_ForDirectMatches()
        {
            var source = @"
class C
{
    void M(System.Collections.Generic.List<Item> list, Item item)
    {
        list.Add(item);
        list.Remove(item);
    }
}";
            var allowedOffset = source.IndexOf("list.Remove", System.StringComparison.Ordinal);
            var occurrences = analyzer.Analyze(source, "list", offset => offset == allowedOffset);

            Assert.That(occurrences.Single().Text, Is.EqualTo("list.Remove(item);"));
        }

        [Test]
        public void Analyze_DoesNotTrackAlias_WhenAliasSourceIsRejected()
        {
            var source = @"
class C
{
    void M(System.Collections.Generic.List<Item> list)
    {
        var item = list[0];
        item.Name = string.Empty;
    }
}";
            var occurrences = analyzer.Analyze(source, "list", static _ => false);

            Assert.That(occurrences, Is.Empty);
        }

        [Test]
        public void Analyze_FindsIndexerAssignmentAsStructuralUsage()
        {
            var occurrences = analyzer.Analyze(@"
class C
{
    void M(Item[] list, Item item)
    {
        list[0] = item;
    }
}", "list");

            Assert.That(occurrences.Single().Kind, Is.EqualTo(CollectionUsageKind.CollectionStructureUsage));
            Assert.That(occurrences.Single().Text, Is.EqualTo("list[0] = item;"));
            Assert.That(occurrences.Single().Length, Is.EqualTo("list[0] =".Length));
        }

        [Test]
        public void Analyze_FindsDirectElementWrites()
        {
            var occurrences = analyzer.Analyze(@"
class C
{
    void M(System.Collections.Generic.List<Item> list)
    {
        list[0].Name = string.Empty;
        list[1].Child.Enabled = false;
    }
}", "list");

            Assert.That(occurrences.Select(static occurrence => occurrence.Kind), Is.EqualTo(new[]
            {
                CollectionUsageKind.ElementWrite,
                CollectionUsageKind.ElementWrite
            }));
        }

        [Test]
        public void Analyze_FindsAliasCreationAndAliasElementWrite()
        {
            var occurrences = analyzer.Analyze(@"
class C
{
    void M(System.Collections.Generic.List<Item> list)
    {
        var item = list[0];
        item.Name = string.Empty;
    }
}", "list");

            Assert.That(occurrences.Select(static occurrence => occurrence.Kind), Is.EqualTo(new[]
            {
                CollectionUsageKind.ElementAlias,
                CollectionUsageKind.ElementWrite
            }));
        }

        [Test]
        public void Analyze_FindsForeachAliasAndElementWrite()
        {
            var occurrences = analyzer.Analyze(@"
class C
{
    void M(System.Collections.Generic.List<Item> list)
    {
        foreach (var item in list)
        {
            item.Enabled = false;
        }
    }
}", "list");

            Assert.That(occurrences.Select(static occurrence => occurrence.Kind), Is.EqualTo(new[]
            {
                CollectionUsageKind.ElementAlias,
                CollectionUsageKind.ElementWrite
            }));
        }

        [Test]
        public void Analyze_FindsEscapes()
        {
            var occurrences = analyzer.Analyze(@"
class C
{
    Item current;

    Item M(System.Collections.Generic.List<Item> list)
    {
        Process(list[0]);
        current = list[1];
        return list[2];
    }
}", "list");

            Assert.That(occurrences.Select(static occurrence => occurrence.Kind), Is.EqualTo(new[]
            {
                CollectionUsageKind.ElementEscape,
                CollectionUsageKind.ElementEscape,
                CollectionUsageKind.ElementEscape
            }));
        }

        [Test]
        public void Analyze_IgnoresCommentsAndStrings()
        {
            var occurrences = analyzer.Analyze(@"
class C
{
    void M(System.Collections.Generic.List<Item> list, Item item)
    {
        // list.Add(item);
        var text = ""list[0].Name = string.Empty;"";
        list.Add(item);
    }
}", "list");

            Assert.That(occurrences.Single().Kind, Is.EqualTo(CollectionUsageKind.CollectionStructureUsage));
            Assert.That(occurrences.Single().Text, Is.EqualTo("list.Add(item);"));
        }

        [Test]
        public void Analyze_IgnoresReadOnlyReferences()
        {
            var occurrences = analyzer.Analyze(@"
class C
{
    void M(System.Collections.Generic.List<Item> list)
    {
        var count = list.Count;
        var firstName = list[0].Name;
        _ = list.Contains(null);
    }
}", "list");

            Assert.That(occurrences, Is.Empty);
        }
    }
}
