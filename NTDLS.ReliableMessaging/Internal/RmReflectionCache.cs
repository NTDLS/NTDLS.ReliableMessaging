using System.Diagnostics.CodeAnalysis;
using System.Reflection;

namespace NTDLS.ReliableMessaging.Internal
{
    /// <summary>
    /// Manages class instances and method reflection information for message handlers.
    /// </summary>
    public class RmReflectionCache
    {
        /// <summary>
        /// Determines the type of method which will be executed.
        /// </summary>
        internal enum CachedMethodType
        {
            /// <summary>
            /// The hander function has only a payload parameter.
            /// </summary>
            PayloadOnly,
            /// <summary>
            /// The hander function has both a context and a payload parameter.
            /// </summary>
            PayloadWithContext
        }

        /// <summary>
        /// An instance of a cached method.
        /// </summary>
        internal class CachedMethod
        {
            /// <summary>
            /// The reflection instance of the cached method.
            /// </summary>
            public MethodInfo Method { get; private set; }

            /// <summary>
            /// The type of the function.
            /// </summary>
            public CachedMethodType MethodType { get; private set; }

            /// <summary>
            /// Creates a new instance of the CachedMethod class.
            /// </summary>
            public CachedMethod(CachedMethodType methodType, MethodInfo method)
            {
                MethodType = methodType;
                Method = method;
            }
        }

        private readonly Dictionary<string, CachedMethod> _handlerMethods = new();
        private readonly Dictionary<Type, IRmMessageHandler> _handlerInstances = new();

        internal void AddInstance(IRmMessageHandler handlerClass)
        {
            _handlerInstances.Add(handlerClass.GetType(), handlerClass);

            LoadConventionBasedHandlerMethods(handlerClass);
        }

        /// <summary>
        /// Calls the appropriate handler function for the given query payload.
        /// </summary>
        /// <returns>Returns true if the function was found and executed. The invocation result is only pending when the handler is asynchronous.</returns>
        internal bool RouteToQueryHander(RmContext context, IRmPayload queryPayload, out ValueTask<IRmQueryReply?> invocationResult)
        {
            //First we try to invoke functions that match the signature, if that fails we will fall back to invoking the OnNotificationReceived() event.
            if (GetCachedMethod(queryPayload, out var cachedMethod))
            {
                var cachedInstance = GetOrCreatedCachedInstance(cachedMethod);

                var method = MakeGenericMethodForPayload(cachedMethod, queryPayload);

                switch (cachedMethod.MethodType)
                {
                    case CachedMethodType.PayloadOnly:
                        invocationResult = ToQueryReply(method.Invoke(cachedInstance, [queryPayload]));
                        return true;
                    case CachedMethodType.PayloadWithContext:
                        invocationResult = ToQueryReply(method.Invoke(cachedInstance, [context, queryPayload]));
                        return true;
                }
            }

            invocationResult = new((IRmQueryReply?)null);

            return false;
        }

        /// <summary>
        /// Converts the return value of a query handler to a reply. Handlers may return either an IRmQueryReply or a Task of one.
        /// </summary>
        private static ValueTask<IRmQueryReply?> ToQueryReply(object? handlerResult)
        {
            if (handlerResult is Task task)
            {
                return task.IsCompletedSuccessfully
                    ? new(GetTaskReply(task))
                    : AwaitTaskReplyAsync(task);
            }

            return new(handlerResult as IRmQueryReply);
        }

        private static async ValueTask<IRmQueryReply?> AwaitTaskReplyAsync(Task task)
        {
            await task.ConfigureAwait(false);
            return GetTaskReply(task);
        }

        /// <summary>
        /// Gets the reply from a completed Task&lt;TReply&gt;. Tasks are not covariant, so we cannot simply cast to Task&lt;IRmQueryReply&gt;.
        /// </summary>
        private static IRmQueryReply? GetTaskReply(Task task)
            => task.GetType().GetProperty(nameof(Task<IRmQueryReply>.Result))?.GetValue(task) as IRmQueryReply;

        /// <summary>
        /// Determines whether a handler return type is valid for a query: either an IRmQueryReply or a Task of one.
        /// </summary>
        private static bool IsQueryReplyReturnType(Type returnType)
        {
            if (typeof(IRmQueryReply).IsAssignableFrom(returnType))
            {
                return true;
            }

            return returnType.IsGenericType
                && returnType.GetGenericTypeDefinition() == typeof(Task<>)
                && typeof(IRmQueryReply).IsAssignableFrom(returnType.GetGenericArguments()[0]);
        }

        /// <summary>
        /// Calls the appropriate handler function for the given notification payload.
        /// </summary>
        /// <returns>Returns true if the function was found and executed.</returns>
        internal bool RouteToNotificationHander(RmContext context, IRmPayload notificationPayload)
        {
            //First we try to invoke functions that match the signature, if that fails we will fall back to invoking the OnNotificationReceived() event.
            if (GetCachedMethod(notificationPayload, out var cachedMethod))
            {
                var cachedInstance = GetOrCreatedCachedInstance(cachedMethod);
                var method = MakeGenericMethodForPayload(cachedMethod, notificationPayload);

                switch (cachedMethod.MethodType)
                {
                    case CachedMethodType.PayloadOnly:
                        method.Invoke(cachedInstance, [notificationPayload]);
                        return true;
                    case CachedMethodType.PayloadWithContext:
                        method.Invoke(cachedInstance, [context, notificationPayload]);
                        return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Creates a cacheable and invokable instance of a handler function by matching generic argument types.
        /// </summary>
        private static MethodInfo MakeGenericMethodForPayload(CachedMethod cachedMethod, IRmPayload payload)
        {
            var payloadType = payload.GetType();

            if (RmCaching.TryGet<MethodInfo>(payloadType, out var cached) && cached != null)
            {
                return cached;
            }

            if (payloadType.IsGenericType && cachedMethod.Method.IsGenericMethod == true)
            {
                //If both the payload and the handler function are generic, We need to create a
                //  generic version of the handler function using the generic types of the payload.

                // Get the generic type definition and its assembly name
                var typeDefinitionName = payloadType.GetGenericTypeDefinition().FullName
                     ?? throw new Exception("The generic type name is not available.");

                var assemblyName = payloadType.Assembly.FullName
                     ?? throw new Exception("The generic assembly type name is not available.");

                // Recursively get the AssemblyQualifiedName of generic arguments
                var genericTypeArguments = payloadType.GetGenericArguments()
                    .Select(t => Type.GetType(t.AssemblyQualifiedName ?? RmReflection.GetAssemblyQualifiedTypeName(t))
                     ?? throw new Exception($"The generic assembly type [{t.AssemblyQualifiedName}] could not be instantiated.")
                    ).ToArray();

                if (genericTypeArguments == null)
                {
                    throw new Exception("The generic assembly type could not be instantiated.");
                }

                var genericMethod = cachedMethod.Method.MakeGenericMethod(genericTypeArguments)
                    ?? throw new Exception("The generic assembly type could not be instantiated.");

                RmCaching.SetOneMinute(payloadType, genericMethod);

                return genericMethod;
            }
            else
            {
                return cachedMethod.Method;
            }
        }

        /// <summary>
        /// Gets the handler class instance from the pre-loaded handler instance cache.
        /// </summary>
        private IRmMessageHandler GetOrCreatedCachedInstance(CachedMethod cachedMethod)
        {
            if (cachedMethod.Method.DeclaringType == null)
            {
                throw new Exception($"The handler function '{cachedMethod.Method.Name}' does not have a container class.");
            }

            if (_handlerInstances.TryGetValue(cachedMethod.Method.DeclaringType, out var cachedInstance) && cachedInstance != null)
            {
                return cachedInstance;
            }
            else
            {
                var newInstance = Activator.CreateInstance(cachedMethod.Method.DeclaringType) as IRmMessageHandler
                    ?? throw new Exception($"Failed to instantiate container class '{cachedMethod.Method.DeclaringType.Name}' for handler function '{cachedMethod.Method.Name}'.");

                _handlerInstances.Add(cachedMethod.Method.DeclaringType, newInstance);
                return newInstance;
            }
        }

        /// <summary>
        /// Gets the handler function from the pre-loaded handler function cache.
        /// </summary>
        private bool GetCachedMethod(IRmPayload payload, [NotNullWhen(true)] out CachedMethod? cachedMethod)
        {
            var typeName = RmReflection.GetAssemblyQualifiedTypeNameWithClosedGenerics(payload);

            if (_handlerMethods.TryGetValue(typeName, out cachedMethod) == false)
            {
                return false;
            }

            if (cachedMethod.Method.DeclaringType == null)
            {
                return false;
            }

            return true;
        }

        /// <summary>
        /// Loads the handler functions from the given handler class.
        /// </summary>
        private void LoadConventionBasedHandlerMethods(IRmMessageHandler handlerClass)
        {
            foreach (var method in handlerClass.GetType().GetMethods())
            {
                var parameters = method.GetParameters();
                if (parameters.Length == 1)
                {
                    if (typeof(IRmPayload).IsAssignableFrom(parameters[0].ParameterType) == false)
                    {
                        continue;
                    }

                    var payloadParameter = parameters[0];
                    if (payloadParameter != null)
                    {
                        //If the payload parameter is a IReliableMessagingQuery, then ensure that the return type is a IReliableMessagingQueryReply.
                        if (RmReflection.ImplementsGenericInterfaceWithArgument(payloadParameter.ParameterType, typeof(IRmQuery<>), typeof(IRmQueryReply)))
                        {
                            if (IsQueryReplyReturnType(method.ReturnType) == false)
                            {
                                continue; //Query handlers must return a IRMQueryReply type (or a Task of one).
                            }
                        }

                        var payloadParameterTypeName = RmReflection.GetAssemblyQualifiedTypeNameWithClosedGenerics(payloadParameter.ParameterType);
                        if (_handlerMethods.ContainsKey(payloadParameterTypeName))
                        {
                            throw new Exception($"Multiple handler functions found for payload type '{payloadParameter.ParameterType.FullName}'. Ensure that only one handler function exists for each payload type.");
                        }
                        _handlerMethods.Add(payloadParameterTypeName, new CachedMethod(CachedMethodType.PayloadOnly, method));
                    }
                }
                else if (parameters.Length == 2)
                {
                    if (typeof(RmContext).IsAssignableFrom(parameters[0].ParameterType) == false)
                    {
                        continue;
                    }

                    if (typeof(IRmPayload).IsAssignableFrom(parameters[1].ParameterType) == false)
                    {
                        continue;
                    }

                    var payloadParameter = parameters[1];
                    if (payloadParameter != null)
                    {
                        //If the payload parameter is a IReliableMessagingQuery, then ensure that the return type is a IReliableMessagingQueryReply.
                        if (RmReflection.ImplementsGenericInterfaceWithArgument(payloadParameter.ParameterType, typeof(IRmQuery<>), typeof(IRmQueryReply)))
                        {
                            if (IsQueryReplyReturnType(method.ReturnType) == false)
                            {
                                continue; //Query handlers must return a IRMQueryReply type (or a Task of one).
                            }
                        }

                        var payloadParameterTypeName = RmReflection.GetAssemblyQualifiedTypeNameWithClosedGenerics(payloadParameter.ParameterType);
                        if (_handlerMethods.ContainsKey(payloadParameterTypeName))
                        {
                            throw new Exception($"Multiple handler functions found for payload type '{payloadParameter.ParameterType.FullName}'. Ensure that only one handler function exists for each payload type.");
                        }
                        _handlerMethods.Add(payloadParameterTypeName, new CachedMethod(CachedMethodType.PayloadWithContext, method));
                    }
                }
            }
        }
    }
}
