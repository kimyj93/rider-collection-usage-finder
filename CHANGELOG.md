# Changelog
All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](http://keepachangelog.com/en/1.0.0/)
and this project adheres to [Semantic Versioning](http://semver.org/spec/v2.0.0.html).

## 1.0.3
- 상세 동작 필터에서 `내용물 수정`과 삭제 계열이 같은 빨강 계열로 보이던 색상 정책을 조정했습니다.
- `내용물 수정`은 보라 계열, `전체 삭제`는 더 명확한 빨강 계열로 분리했습니다.

## 1.0.2
- 읽기 계열 사용 위치 탐지와 `읽기` 대분류/상세 필터를 추가했습니다.
- 읽기 계열은 기존 수정·대입·레퍼런스 탐지 결과를 방해하지 않도록 기본 Off로 시작합니다.

## 1.0.1
- 대분류 필터를 하위 필터와 구분되는 탭 형태로 조정했습니다.
- 꺼진 대분류에 속한 상세 필터는 숨기도록 조정했습니다.

## 1.0.0
- C# BCL 컬렉션 전용 사용 위치를 검색하는 Rider 액션을 추가했습니다.
- Rider 2024.3 계열, build 243 이상을 지원합니다.
- `List`, `IList`, `ICollection`, `Collection`, `Dictionary`, `IDictionary`, `HashSet`, `ISet`, `Queue`, `Stack`, 배열을 지원합니다.
- 원소 추가, 원소 삭제, 전체 삭제, 순서 변경, 집합 연산, 큐/스택 연산, 인덱서 교체 같은 컬렉션 구조 변경을 감지합니다.
- 컬렉션 자체 대입, 컬렉션 원소의 내용물 수정, 원소 alias 생성, 단순 원소 escape를 감지합니다.
- 결과 수, 대분류 필터, 상세 동작 필터, 접기/펼치기, 코드 이동, Rider 스타일 코드 프리뷰를 제공하는 전용 팝업을 추가했습니다.
- 전용 팝업에서 Rider 기본 Find Usages로 넘어가는 `전체 사용 위치 찾기` 버튼을 추가했습니다.
- Rider/ReSharper 버전에 따라 generic read lock API를 찾지 못하던 백엔드 실행 호환성 문제를 수정했습니다.
- 기본 단축키 `Ctrl+F12`와 에디터 우클릭 메뉴 항목을 추가했습니다.
