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
        public void Analyze_FindsNullConditionalStructuralUsages()
        {
            var occurrences = analyzer.Analyze(@"
class C
{
    void M(System.Collections.Generic.Dictionary<int, Item> map)
    {
        map?.Clear();
    }
}", "map");

            Assert.That(occurrences.Single().Kind, Is.EqualTo(CollectionUsageKind.CollectionStructureUsage));
            Assert.That(occurrences.Single().Text, Is.EqualTo("map?.Clear();"));
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
        public void Analyze_DoesNotTreatDirectIndexerComparisonsAsWrites()
        {
            var occurrences = analyzer.Analyze(@"
class C
{
    void M(System.Collections.Generic.List<Item> list, string name, Item item)
    {
        if (list[0].Name == name)
            return;

        if (list[1] == item)
            return;
    }
}", "list");

            Assert.That(occurrences, Is.Empty);
        }

        [Test]
        public void Analyze_FindsCompoundMutationOperators()
        {
            var occurrences = analyzer.Analyze(@"
class C
{
    void M(System.Collections.Generic.List<Item> list, Item item)
    {
        list[0].Amount += 1;
        list[1].Amount++;
        list[2] ??= item;
    }
}", "list");

            Assert.That(occurrences.Select(static occurrence => occurrence.Kind), Is.EqualTo(new[]
            {
                CollectionUsageKind.ElementWrite,
                CollectionUsageKind.ElementWrite,
                CollectionUsageKind.CollectionStructureUsage
            }));
        }

        [Test]
        public void Analyze_FindsDictionaryElementWritesWithNestedIndexerInKey()
        {
            var occurrences = analyzer.Analyze(@"
class C
{
    void M(System.Collections.Generic.Dictionary<int, Item> map, System.Collections.Generic.List<Info> infoList, int i)
    {
        map[infoList[i].Id].FreeAmount = infoList[i].FreeStack;
    }
}", "map");

            Assert.That(occurrences.Single().Kind, Is.EqualTo(CollectionUsageKind.ElementWrite));
            Assert.That(occurrences.Single().Text, Is.EqualTo("map[infoList[i].Id].FreeAmount = infoList[i].FreeStack;"));
        }

        [Test]
        public void Analyze_FindsDictionaryElementWritesWithDeepNestedIndexerInKey()
        {
            var occurrences = analyzer.Analyze(@"
class C
{
    void M(System.Collections.Generic.Dictionary<int, Item> map, int[] a, int[] b, int[] c, int i)
    {
        map[a[b[c[i]]]].Amount = 1;
    }
}", "map");

            Assert.That(occurrences.Single().Kind, Is.EqualTo(CollectionUsageKind.ElementWrite));
            Assert.That(occurrences.Single().Text, Is.EqualTo("map[a[b[c[i]]]].Amount = 1;"));
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
        public void Analyze_DoesNotApplyAliasOutsideLexicalScope()
        {
            var occurrences = analyzer.Analyze(@"
class C
{
    void FromCollection(System.Collections.Generic.Dictionary<int, Item> _brokenItems, int key)
    {
        var item = _brokenItems[key];
        item.Name = string.Empty;
    }

    Item CreateItem(Info itemInfo)
    {
        var item = CreateNewItem();
        item.UID = itemInfo.Id;
        item.Amount = itemInfo.Amount;
        return item;
    }
}", "_brokenItems");

            Assert.That(occurrences.Select(static occurrence => occurrence.Text), Is.EqualTo(new[]
            {
                "var item = _brokenItems[key];",
                "item.Name = string.Empty;"
            }));
        }

        [Test]
        public void Analyze_DoesNotTreatAliasComparisonsAsWrites()
        {
            var occurrences = analyzer.Analyze(@"
class C
{
    Item GetBrokenItemByUid(System.Collections.Generic.Dictionary<int, Item> _brokenItems, int uid)
    {
        foreach (var item in _brokenItems)
        {
            if (item.Key == uid)
                return item.Value;
        }

        return null;
    }
}", "_brokenItems");

            Assert.That(occurrences.Select(static occurrence => occurrence.Kind), Is.EqualTo(new[]
            {
                CollectionUsageKind.ElementAlias
            }));
            Assert.That(occurrences.Single().Text, Is.EqualTo("foreach (var item in _brokenItems)"));
        }

        [Test]
        public void Analyze_DoesNotTreatAliasRelationalComparisonsAsWrites()
        {
            var occurrences = analyzer.Analyze(@"
class C
{
    void M(System.Collections.Generic.Dictionary<int, Item> map)
    {
        foreach (var item in map)
        {
            if (item.Value.Amount >= 1)
                return;

            if (item.Value.Amount <= 10)
                return;

            if (item.Value != null)
                return;
        }
    }
}", "map");

            Assert.That(occurrences.Select(static occurrence => occurrence.Kind), Is.EqualTo(new[]
            {
                CollectionUsageKind.ElementAlias
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
        public void Analyze_DoesNotApplyForeachAliasOutsideLoopBody()
        {
            var occurrences = analyzer.Analyze(@"
class C
{
    void FromCollection(System.Collections.Generic.List<Item> list)
    {
        foreach (var item in list)
        {
            item.Name = string.Empty;
        }

        var item = CreateNewItem();
        item.UID = 1;
    }
}", "list");

            Assert.That(occurrences.Select(static occurrence => occurrence.Text), Is.EqualTo(new[]
            {
                "foreach (var item in list)",
                "item.Name = string.Empty;"
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
        public void Analyze_ReportsEscapeAtCollectionElementInsteadOfStatementPrefix()
        {
            var source = @"
class C
{
    Item previousItem;

    void M(System.Collections.Generic.List<Item> items, int slot)
    {
        {
            // comment before escape
            previousItem = items[slot];
        }
    }
}";

            var occurrence = analyzer.Analyze(source, "items").Single();

            Assert.That(occurrence.Kind, Is.EqualTo(CollectionUsageKind.ElementEscape));
            Assert.That(occurrence.Text, Is.EqualTo("previousItem = items[slot];"));
            Assert.That(occurrence.StartOffset, Is.EqualTo(source.IndexOf("items[slot]", System.StringComparison.Ordinal)));
            Assert.That(occurrence.Line, Is.EqualTo(10));
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
