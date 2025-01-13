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

            var objectType = CompiledRegEx.TypeTagsRegex().Replace(assemblyQualifiedName, string.Empty);
            objectType = CompiledRegEx.TypeCleanupRegex().Replace(objectType, ", ").Trim();

            return objectType;
        }

        public static string GetAssemblyQualifiedTypeNameWithClosedGenerics(object obj)
        {
            return GetAssemblyQualifiedTypeNameWithClosedGenerics(obj.GetType());
        }

        public static string GetAssemblyQualifiedTypeNameWithClosedGenerics(Type type)
        {
            string assemblyQualifiedName;

            if (type.IsGenericType)
            {
                var typeDefinitionName = type.GetGenericTypeDefinition().FullName
                     ?? throw new Exception("The generic type name is not available.");

                var assemblyName = type.Assembly.FullName
                     ?? throw new Exception("The generic assembly type name is not available.");

                var genericArguments = type.GetGenericArguments()
                    .Select(t => t.AssemblyQualifiedName ?? GetAssemblyQualifiedTypeNameWithClosedGenerics(t));

                // Recursively get the AssemblyQualifiedName of generic arguments
                string genericArgumentsString = '[' + string.Join("], [", genericArguments) + ']';

                assemblyQualifiedName  = $"{typeDefinitionName}[{genericArgumentsString}], {assemblyName}";
            }
            else
            {
                assemblyQualifiedName = type.AssemblyQualifiedName ?? type.Name
                    ?? throw new Exception("The type name is not available.");
            }

            var objectType = CompiledRegEx.TypeTagsRegex().Replace(assemblyQualifiedName, string.Empty);
            objectType = CompiledRegEx.TypeCleanupRegex().Replace(objectType, ", ").Trim();

            return objectType;
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
