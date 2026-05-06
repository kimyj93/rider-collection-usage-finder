# CollectionUsageFinder

CollectionUsageFinder는 Rider에서 C# BCL 컬렉션의 전용 사용 위치를 찾아주는 플러그인입니다.
기본 Find Usages의 read/write 분류로는 구분하기 어려운 원소 추가/삭제, 내용물 수정,
컬렉션 대입, 레퍼런스 넘기기 위치를 별도 팝업에서 보여줍니다.

## 사용 방법

지원되는 컬렉션 심볼 위에 커서를 둔 뒤 다음 중 하나로 실행합니다.

- 단축키: `Ctrl+F12`
- 에디터 우클릭 메뉴: `컬렉션 사용 위치 찾기`
- Action Search: `컬렉션 사용 위치 찾기`

Diagnostics 액션은 1.0 배포판에서 메뉴에 노출하지 않습니다. 소스는 남겨두었기 때문에
문제 진단이 필요하면 다시 등록해서 사용할 수 있습니다.

## 지원 대상

언어와 타입 범위는 의도적으로 좁게 잡았습니다.

- Rider 2024.3 계열, build 243 이상
- C# only
- BCL mutable collections only
- 기존 Rider Find Usages를 대체하지 않는 별도 액션

지원 컬렉션:

- `List<T>`, `IList<T>`, `ICollection<T>`, `Collection<T>`
- `Dictionary<TKey, TValue>`, `IDictionary<TKey, TValue>`
- `HashSet<T>`, `ISet<T>`
- `Queue<T>`, `Stack<T>`
- 배열

## 검색 범위

- 지역 변수와 파라미터: 현재 파일
- 필드와 프로퍼티: 선언된 프로젝트의 C# 파일

## 결과 분류

결과는 전용 팝업에서 대분류와 상세 동작으로 나뉩니다.

- `원소 추가/삭제`: `Add`, `TryAdd`, `Remove`, `RemoveAll`, `Clear`, `Enqueue`, `Push` 등
- `컬렉션 대입`: 컬렉션 변수/필드/프로퍼티 자체를 다른 컬렉션으로 대입
- `내용물 수정`: `list[i].Name = ...`, `dictionary[key].Amount += ...` 같은 원소 내부 변경
- `레퍼런스 넘기기`: `var item = list[i]`, `foreach`, 인자 전달, 반환 등

팝업은 전체/필터링된 결과 수, 대분류/상세 필터, 접기/펼치기, 코드 프리뷰를 제공합니다.

## Spec

- [MVP spec](docs/mvp-spec.md)

## Build

```powershell
dotnet test src\dotnet\ReSharperPlugin.CollectionUsageFinder.Tests\ReSharperPlugin.CollectionUsageFinder.Tests.csproj -v minimal -m:1
.\gradlew.bat buildPlugin --no-daemon
```
