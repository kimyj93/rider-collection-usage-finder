# CollectionUsageFinder

Rider/ReSharper plugin prototype for C# that adds a collection-specific search action for BCL mutable collections.

## Current Status

The repository currently contains:

- an official JetBrains ReSharper/Rider plugin scaffold
- a working solution and plugin project layout
- an MVP specification for the first searchable feature set
- a `Find Collection Usages` action enabled on supported C# BCL collection symbols
- a default Rider keymap shortcut: `Ctrl+F12`
- a dedicated popup for detected collection structure usages, element writes, aliases, and escapes
- result count, category filters, and a code preview pane in the popup

Current search scope:

- local variables and parameters: current file
- fields and properties: C# files in the declaring project

## Usage

Place the caret on a collection local variable, parameter, field, or property whose type is
`List<T>`, `IList<T>`, `ICollection<T>`, `Collection<T>`, `Dictionary<TKey,TValue>`,
`IDictionary<TKey,TValue>`, `HashSet<T>`, `ISet<T>`, `Queue<T>`, `Stack<T>`, or an array, then run
`Find Collection Usages`.

The action is registered with the Find Usages menu groups, not the Tools menu:

- default shortcut: `Ctrl+F12`
- action search: search for `Find Collection Usages`
- editor context menu / Find Usages area when the caret is on a supported collection symbol

Results open in a CollectionUsageFinder popup grouped by usage category:

- `원소 추가/삭제`
- `내용물 수정`
- `레퍼런스 넘기기`

The popup also shows the total/filtered result count, checkboxes for category filtering, and a read-only
preview of the selected result's surrounding code.

## Spec

- [MVP spec](docs/mvp-spec.md)

## Scope

The initial target is intentionally narrow:

- C# only
- BCL mutable collections only
- a new collection-specific action, not a replacement for existing Find Usages

## Build

```powershell
dotnet test src\dotnet\ReSharperPlugin.CollectionUsageFinder.Tests\ReSharperPlugin.CollectionUsageFinder.Tests.csproj -v minimal -m:1
.\gradlew.bat buildPlugin --no-daemon
```
