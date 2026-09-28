using System.Collections.Frozen;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.Json;

namespace Meteo.TestSupport;

/// <summary>
/// TP3, « pas de static » : liste les champs statiques capables de porter un état partagé
/// entre requêtes. Sont tolérés les constantes et les champs readonly d'un type immuable :
/// primitifs, enum, string, FrozenSet/FrozenDictionary, délégués (callbacks LoggerMessage
/// générés), JsonSerializerOptions (figées au premier usage). Les types générés par le
/// compilateur (caches de lambdas, machines à états) sont ignorés, mais les champs support des
/// propriétés statiques ne le sont pas : une propriété statique Dictionary est aussi signalée.
/// </summary>
public static class StaticStateInspector
{
    public static IReadOnlyList<string> FindMutableStaticFields(IEnumerable<Type> types) =>
    [
        .. (from type in types
            where !IsCompilerGenerated(type)
            from field in type.GetFields(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly)
            where !field.IsLiteral && !(field.IsInitOnly && IsImmutable(field.FieldType))
            select $"{type.FullName}.{field.Name} ({field.FieldType.Name})"),
    ];

    private static bool IsCompilerGenerated(Type? type)
    {
        for (; type is not null; type = type.DeclaringType)
        {
            if (type.IsDefined(typeof(CompilerGeneratedAttribute), inherit: false))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsImmutable(Type type) =>
        type.IsPrimitive
        || type.IsEnum
        || type == typeof(string)
        || type == typeof(JsonSerializerOptions)
        || typeof(Delegate).IsAssignableFrom(type)
        || (type.IsGenericType
            && (type.GetGenericTypeDefinition() == typeof(FrozenSet<>)
                || type.GetGenericTypeDefinition() == typeof(FrozenDictionary<,>)));
}
