using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using JetBrains.Annotations;
using JetBrains.ReSharper.Psi;

namespace ReSharperPlugin.CollectionUsageFinder.Search
{
    public static class CollectionSupport
    {
        private static readonly Dictionary<string, SupportedCollectionKind> SupportedClrTypeNames =
            new Dictionary<string, SupportedCollectionKind>(StringComparer.Ordinal)
            {
                { "System.Collections.Generic.List", SupportedCollectionKind.List },
                { "System.Collections.Generic.List`1", SupportedCollectionKind.List },
                { "System.Collections.Generic.ICollection", SupportedCollectionKind.ICollection },
                { "System.Collections.Generic.ICollection`1", SupportedCollectionKind.ICollection },
                { "System.Collections.Generic.IList", SupportedCollectionKind.IList },
                { "System.Collections.Generic.IList`1", SupportedCollectionKind.IList },
                { "System.Collections.ObjectModel.Collection", SupportedCollectionKind.Collection },
                { "System.Collections.ObjectModel.Collection`1", SupportedCollectionKind.Collection },
                { "System.Collections.Generic.Dictionary", SupportedCollectionKind.Dictionary },
                { "System.Collections.Generic.Dictionary`2", SupportedCollectionKind.Dictionary },
                { "System.Collections.Generic.IDictionary", SupportedCollectionKind.Dictionary },
                { "System.Collections.Generic.IDictionary`2", SupportedCollectionKind.Dictionary },
                { "System.Collections.Generic.HashSet", SupportedCollectionKind.HashSet },
                { "System.Collections.Generic.HashSet`1", SupportedCollectionKind.HashSet },
                { "System.Collections.Generic.ISet", SupportedCollectionKind.ISet },
                { "System.Collections.Generic.ISet`1", SupportedCollectionKind.ISet },
                { "System.Collections.Generic.Queue", SupportedCollectionKind.Queue },
                { "System.Collections.Generic.Queue`1", SupportedCollectionKind.Queue },
                { "System.Collections.Generic.Stack", SupportedCollectionKind.Stack },
                { "System.Collections.Generic.Stack`1", SupportedCollectionKind.Stack },
            };

        [CanBeNull]
        public static CollectionSearchTarget TryCreateTarget([CanBeNull] IDeclaredElement declaredElement)
        {
            if (declaredElement == null)
                return null;

            var targetKind = TryGetTargetKind(declaredElement);
            if (targetKind == null)
                return null;

            var collectionKind = TryGetSupportedCollectionKind(TryGetElementType(declaredElement));
            if (collectionKind == null)
                return null;

            return new CollectionSearchTarget(
                declaredElement,
                GetDisplayName(declaredElement),
                targetKind.Value,
                collectionKind.Value);
        }

        [CanBeNull]
        public static CollectionSearchTargetKind? TryGetTargetKind([NotNull] IDeclaredElement declaredElement)
        {
            var interfaceNames = declaredElement
                .GetType()
                .GetInterfaces()
                .Select(static type => type.FullName)
                .Where(static name => name != null)
                .ToHashSet(StringComparer.Ordinal);

            if (interfaceNames.Contains("JetBrains.ReSharper.Psi.ILocalVariable"))
                return CollectionSearchTargetKind.LocalVariable;

            if (interfaceNames.Contains("JetBrains.ReSharper.Psi.IField"))
                return CollectionSearchTargetKind.Field;

            if (interfaceNames.Contains("JetBrains.ReSharper.Psi.IProperty"))
                return CollectionSearchTargetKind.Property;

            if (interfaceNames.Contains("JetBrains.ReSharper.Psi.IParameter"))
                return CollectionSearchTargetKind.Parameter;

            return null;
        }

        public static bool IsSupportedCollectionClrName([CanBeNull] string clrTypeName)
        {
            return TryGetSupportedCollectionKind(clrTypeName, isArray: false) != null;
        }

        [CanBeNull]
        public static SupportedCollectionKind? TryGetSupportedCollectionKind([CanBeNull] string clrTypeName, bool isArray)
        {
            if (isArray)
                return SupportedCollectionKind.Array;

            if (clrTypeName == null)
                return null;

            return SupportedClrTypeNames.TryGetValue(clrTypeName, out var collectionKind)
                ? collectionKind
                : (SupportedCollectionKind?)null;
        }

        [CanBeNull]
        public static SupportedCollectionKind? TryGetSupportedCollectionKind([CanBeNull] IType type)
        {
            if (type == null)
                return null;

            if (type is IArrayType)
                return SupportedCollectionKind.Array;

            if (type is IDeclaredType declaredType)
                return TryGetSupportedCollectionKind(TryGetClrTypeName(declaredType), isArray: false);

            return null;
        }

        [CanBeNull]
        private static IType TryGetElementType([NotNull] IDeclaredElement declaredElement)
        {
            var typeProperty = declaredElement.GetType().GetProperty(
                "Type",
                BindingFlags.Instance | BindingFlags.Public);

            return typeProperty?.GetValue(declaredElement, null) as IType;
        }

        [NotNull]
        private static string GetDisplayName([NotNull] IDeclaredElement declaredElement)
        {
            var shortNameProperty = declaredElement.GetType().GetProperty(
                "ShortName",
                BindingFlags.Instance | BindingFlags.Public);

            if (shortNameProperty?.GetValue(declaredElement, null) is string shortName &&
                !string.IsNullOrWhiteSpace(shortName))
            {
                return shortName;
            }

            return declaredElement.GetType().Name;
        }

        [CanBeNull]
        private static string TryGetClrTypeName([NotNull] IDeclaredType declaredType)
        {
            try
            {
                var clrTypeName = declaredType.GetClrName();
                if (clrTypeName == null)
                    return null;

                var fullNameProperty = clrTypeName.GetType().GetProperty(
                    "FullName",
                    BindingFlags.Instance | BindingFlags.Public);

                if (fullNameProperty?.GetValue(clrTypeName, null) is string fullName &&
                    !string.IsNullOrWhiteSpace(fullName))
                {
                    return fullName;
                }

                var renderedName = clrTypeName.ToString();
                return string.IsNullOrWhiteSpace(renderedName) ? null : renderedName;
            }
            catch
            {
                return null;
            }
        }
    }
}
