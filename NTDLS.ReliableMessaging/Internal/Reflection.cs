namespace NTDLS.ReliableMessaging.Internal
{
    internal class Reflection
    {
        public static string GetAssemblyQualifiedTypeName(object obj)
        {
            return GetAssemblyQualifiedTypeName(obj.GetType());
        }

        public static string GetAssemblyQualifiedTypeName(Type type)
        {
            if (Caching.CacheTryGet<string>($"AQT:{type}", out var objectTypeName) && objectTypeName != null)
            {
                return objectTypeName;
            }

            string assemblyQualifiedName;

            if (type.IsGenericType)
            {
                var typeDefinitionName = type.GetGenericTypeDefinition().FullName
                     ?? throw new Exception("The generic type name is not available.");

                var assemblyName = type.Assembly.FullName
                     ?? throw new Exception("The generic assembly type name is not available.");

                assemblyQualifiedName = $"{typeDefinitionName}, {assemblyName}";
            }
            else
            {
                assemblyQualifiedName = type.AssemblyQualifiedName ?? type.Name
                    ?? throw new Exception("The type name is not available.");
            }

            objectTypeName = CompiledRegEx.TypeTagsRegex().Replace(assemblyQualifiedName, string.Empty);
            objectTypeName = CompiledRegEx.TypeCleanupRegex().Replace(objectTypeName, ", ").Trim();

            Caching.CacheSetOneMinute(type, objectTypeName);

            return objectTypeName;
        }

        public static string GetAssemblyQualifiedTypeNameWithClosedGenerics(object obj)
        {
            return GetAssemblyQualifiedTypeNameWithClosedGenerics(obj.GetType());
        }

        public static string GetAssemblyQualifiedTypeNameWithClosedGenerics(Type type)
        {
            if (Caching.CacheTryGet<string>($"AQT_WCT:{type}", out var objectTypeName) && objectTypeName != null)
            {
                return objectTypeName;
            }

            string assemblyQualifiedName;

            if (type.IsGenericType)
            {
                var typeDefinitionName = type.GetGenericTypeDefinition().FullName
                     ?? throw new Exception("The generic type name is not available.");

                var assemblyName = type.Assembly.FullName
                     ?? throw new Exception("The generic assembly type name is not available.");

                // Recursively get the AssemblyQualifiedName of generic arguments
                var genericArguments = type.GetGenericArguments()
                    .Select(t => t.AssemblyQualifiedName ?? GetAssemblyQualifiedTypeNameWithClosedGenerics(t));

                string genericArgumentsString = '[' + string.Join("], [", genericArguments) + ']';

                assemblyQualifiedName  = $"{typeDefinitionName}[{genericArgumentsString}], {assemblyName}";
            }
            else
            {
                assemblyQualifiedName = type.AssemblyQualifiedName ?? type.Name
                    ?? throw new Exception("The type name is not available.");
            }

            objectTypeName = CompiledRegEx.TypeTagsRegex().Replace(assemblyQualifiedName, string.Empty);
            objectTypeName = CompiledRegEx.TypeCleanupRegex().Replace(objectTypeName, ", ").Trim();

            Caching.CacheSetOneMinute(type, objectTypeName);

            return objectTypeName;
        }

        public static bool ImplementsGenericInterfaceWithArgument(Type type, Type genericInterface, Type argumentType)
        {
            return type.GetInterfaces().Any(interfaceType =>
                interfaceType.IsGenericType &&
                interfaceType.GetGenericTypeDefinition() == genericInterface &&
                interfaceType.GetGenericArguments().Any(arg => argumentType.IsAssignableFrom(arg)));
        }
    }
}
