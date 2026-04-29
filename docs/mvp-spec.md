# CollectionUsageFinder MVP Spec

## Capability

CollectionUsageFinder adds a Rider/ReSharper action for C# code that searches for collection-specific writes and element handoff patterns that the built-in read/write usage split does not express well for mutable BCL collections.

The MVP focuses on answering this question:

> "Given this collection, where is its structure changed, where is one of its elements written through, and where does an element escape into another reference?"

## Product Goals

- Provide a collection-specific search that is more useful than default read/write usage classification for mutable collections.
- Make common collection usage sites easy to review from a single results tab.
- Surface element aliasing/escape sites that often hide later writes.
- Keep the first version narrow enough to validate whether the workflow is worth deepening.

## Non-Goals

- Replacing or patching Rider's existing `Find Usages` behavior.
- Whole-program points-to analysis.
- Precise interprocedural usage/write tracking across arbitrary method calls.
- Support for user-defined collection types in MVP.
- Support for reflection, `dynamic`, unsafe code, or third-party libraries in MVP.

## Supported Language and Type Scope

### Language

- C# only

### Collection Families

Mutable BCL collection shapes only:

- `System.Collections.Generic.List<T>`
- `System.Collections.Generic.ICollection<T>`
- `System.Collections.Generic.IList<T>`
- `System.Collections.ObjectModel.Collection<T>`
- `System.Collections.Generic.Dictionary<TKey, TValue>`
- `System.Collections.Generic.IDictionary<TKey, TValue>`
- `System.Collections.Generic.HashSet<T>`
- `System.Collections.Generic.ISet<T>`
- `System.Collections.Generic.Queue<T>`
- `System.Collections.Generic.Stack<T>`
- arrays (`T[]`)

### Explicitly Out of Scope for MVP

- immutable collections
- concurrent collections
- linked lists
- custom collection implementations

## Search Entry Points

The action is available when the caret is on a collection symbol of one of these kinds:

- local variable
- field
- property
- parameter

## Search Categories

Results are grouped into the following logical categories.

### 1. Collection Structure Usage

Operations that change the collection container itself.

Examples:

```csharp
list.Add(item);
list.AddRange(items);
list.Insert(index, item);
list.Remove(item);
list.RemoveAt(index);
list.RemoveRange(start, count);
list.Clear();
list.Sort();
list.Reverse();
dictionary.TryAdd(key, value);
list.RemoveAll(predicate);
set.RemoveWhere(predicate);
set.UnionWith(other);
set.IntersectWith(other);
set.ExceptWith(other);
set.SymmetricExceptWith(other);
queue.Enqueue(item);
queue.Dequeue();
queue.TryDequeue(out var item);
stack.Push(item);
stack.Pop();
stack.TryPop(out var item);
list[i] = replacement;
array[i] = replacement;
```

### 2. Element Write

Writes to a member of an element that is read from the collection within the same member body.

Examples:

```csharp
list[i].Name = string.Empty;
array[index].IsEnabled = false;
```

### 3. Element Alias

A local reference or foreach variable is created from a collection element.

Examples:

```csharp
var item = list[i];
var current = array[index];

foreach (var entry in list)
{
}
```

### 4. Element Escape

A collection element is passed or returned in a way that makes later writes harder to inspect locally.

Examples:

```csharp
Process(list[i]);
return list[i];
_current = list[i];
```

For MVP, `Escape` is an intentional "warning bucket", not proof that a write happened later.

## Supported Analysis Rules

### Direct Collection Usage

The plugin should report:

- direct method calls on the target collection for a whitelisted set of known structure-changing members
- indexer assignment on supported indexed collections

### Direct Element Write

The plugin should report:

- member assignment where the assignment target is rooted in a supported collection element access

Supported shape:

```csharp
list[i].Name = value;
```

### Local Alias Tracking

The plugin should report alias creation for:

- local variables initialized from `list[index]` or `array[index]`
- foreach iteration variables sourced from the target collection

The plugin may then use those aliases to detect later member writes inside the same containing member.

Supported shape:

```csharp
var item = list[i];
item.Name = value;
```

### Escape Detection

The plugin should report simple escape sites for elements obtained from the target collection:

- argument passing
- return statements
- assignment to a field or property

## Deliberate MVP Limits

The MVP does not attempt to be clever in the following cases:

- alias chains longer than one local step
- lambda capture
- async/iterator state machine flow
- usage inferred only through unknown method bodies
- extension methods except explicitly whitelisted BCL structure-changing members
- element identity tracking after reordering operations
- branch-sensitive or loop-sensitive reasoning beyond obvious local syntax

When the plugin cannot prove a write but sees an escape, it should emit `Element Escape` and stop there.

## UX

### Primary Action

- `Find Collection Usages`
- default shortcut: `Ctrl+F12`

### Optional Follow-up Action

- `Find Collection Usages Advanced...`

The advanced action is not required for the first implementation.

### Invocation Surfaces

- editor context menu
- Action Search
- optional `Alt+Enter` integration later

### Results UI

Use the existing ReSharper/Rider `Find Results` window.

Reasons:

- matches the user's existing search workflow
- supports navigation, tabs, grouping, and filtering already
- avoids building a custom tool window before the feature proves itself

### Result Grouping

Preferred top-level groups:

- `Collection Structure Usage`
- `Element Write`
- `Element Alias`
- `Element Escape`

## Technical Shape

### Backend

Implement the first version in the ReSharper backend using C# PSI/search infrastructure.

Likely pieces:

- a context search provider / action entry point
- collection symbol/type classifier
- local PSI walker for pattern collection
- custom occurrences and occurrence kind mapping for `Find Results`

### Frontend

Keep Rider frontend work minimal for MVP.

- no custom RD model required unless the chosen action wiring demands it
- no custom tool window in v1

## Acceptance Criteria

The MVP is successful if all of the following are true:

- invoking the action on a supported collection symbol opens results in `Find Results`
- direct container usages are found reliably
- direct element writes such as `list[i].Name = ...` are found
- alias creation such as `var item = list[i]` is found
- simple escapes such as `Process(list[i])` are found
- unsupported cases fail conservatively rather than reporting wrong "write" results

## First Implementation Order

1. Action entry point that is only enabled for supported C# collection symbols.
2. Type filter for supported BCL collection shapes.
3. Direct container usage search.
4. Direct element write detection from collection element access.
5. Local alias detection.
6. Simple escape detection.
7. Result grouping polish.

## Open Questions for Later

- whether `Element Alias` results should be suppressible as noise
- whether dictionary-like collections deserve a separate mode
- whether "possible downstream usage" should become a separate category later
- whether inspection/highlighting should be added after search UX is validated
