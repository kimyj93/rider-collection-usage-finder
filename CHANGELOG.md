# Changelog
All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](http://keepachangelog.com/en/1.0.0/)
and this project adheres to [Semantic Versioning](http://semver.org/spec/v2.0.0.html).

## 1.0.0
- C# BCL 컬렉션 전용 사용 위치를 검색하는 Rider 액션을 추가했습니다.
- Rider 2024.3 계열, build 243 이상을 지원합니다.
- `List`, `IList`, `ICollection`, `Collection`, `Dictionary`, `IDictionary`, `HashSet`, `ISet`, `Queue`, `Stack`, 배열을 지원합니다.
- 원소 추가, 원소 삭제, 전체 삭제, 순서 변경, 집합 연산, 큐/스택 연산, 인덱서 교체 같은 컬렉션 구조 변경을 감지합니다.
- 컬렉션 자체 대입, 컬렉션 원소의 내용물 수정, 원소 alias 생성, 단순 원소 escape를 감지합니다.
- 결과 수, 대분류 필터, 상세 동작 필터, 접기/펼치기, 코드 이동, Rider 스타일 코드 프리뷰를 제공하는 전용 팝업을 추가했습니다.
- 기본 단축키 `Ctrl+F12`와 에디터 우클릭 메뉴 항목을 추가했습니다.
