using JetBrains.Annotations;

namespace ReSharperPlugin.CollectionUsageFinder.Search
{
    internal static class CollectionUsageOperations
    {
        public static CollectionUsageOperationKind FromStructuralMemberName([NotNull] string memberName)
        {
            switch (memberName)
            {
                case "Add":
                case "AddRange":
                case "Insert":
                case "InsertRange":
                case "TryAdd":
                case "Enqueue":
                case "Push":
                    return CollectionUsageOperationKind.ElementAdd;

                case "Clear":
                    return CollectionUsageOperationKind.ElementClear;

                case "Remove":
                case "RemoveAt":
                case "RemoveRange":
                case "RemoveAll":
                case "RemoveWhere":
                case "Dequeue":
                case "TryDequeue":
                case "Pop":
                case "TryPop":
                    return CollectionUsageOperationKind.ElementRemove;

                case "Sort":
                case "Reverse":
                    return CollectionUsageOperationKind.CollectionReorder;

                case "UnionWith":
                case "IntersectWith":
                case "ExceptWith":
                case "SymmetricExceptWith":
                    return CollectionUsageOperationKind.SetOperation;

                default:
                    return CollectionUsageOperationKind.ElementSet;
            }
        }

        [NotNull]
        public static string ToDisplayName(CollectionUsageOperationKind operationKind)
        {
            switch (operationKind)
            {
                case CollectionUsageOperationKind.ElementAdd:
                    return "원소 추가";

                case CollectionUsageOperationKind.ElementRemove:
                    return "원소 삭제";

                case CollectionUsageOperationKind.ElementClear:
                    return "전체 삭제";

                case CollectionUsageOperationKind.ElementSet:
                    return "인덱서 설정/교체";

                case CollectionUsageOperationKind.CollectionReorder:
                    return "순서 변경";

                case CollectionUsageOperationKind.SetOperation:
                    return "집합 연산";

                case CollectionUsageOperationKind.CollectionInitialization:
                    return "컬렉션 초기화";

                case CollectionUsageOperationKind.CollectionAssignment:
                    return "컬렉션 대입";

                case CollectionUsageOperationKind.ElementContentWrite:
                    return "내용물 수정";

                case CollectionUsageOperationKind.ElementReference:
                    return "원소 레퍼런스";

                case CollectionUsageOperationKind.CollectionReference:
                    return "컬렉션 레퍼런스";

                case CollectionUsageOperationKind.ByRefCollectionReference:
                    return "참조 인자";

                case CollectionUsageOperationKind.ElementRead:
                    return "원소 읽기";

                case CollectionUsageOperationKind.ConditionRead:
                    return "상태/조건 확인";

                case CollectionUsageOperationKind.ConditionComparisonRead:
                    return "조건 비교";

                case CollectionUsageOperationKind.EnumerationRead:
                    return "순회";

                case CollectionUsageOperationKind.CopyRead:
                    return "복사/뷰";

                case CollectionUsageOperationKind.QueryRead:
                    return "쿼리";

                case CollectionUsageOperationKind.DirectElementMethodCall:
                    return "직접 호출";

                default:
                    return "컬렉션 사용";
            }
        }
    }
}
