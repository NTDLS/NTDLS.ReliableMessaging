﻿using System.Diagnostics.CodeAnalysis;
using System.Reflection;

namespace NTDLS.ReliableMessaging.Internal
{
    /// <summary>
    /// Manages class instances and method reflection information for message handlers.
    /// </summary>
    public class ReflectionCache
    {
        /// <summary>
        /// Determines the type of method which will be executed.
        /// </summary>
        public enum CachedMethodType
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
        public class CachedMethod
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
            /// <param name="methodType"></param>
            /// <param name="method"></param>
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
        /// <returns>Returns true if the function was found and executed.</returns>
        internal bool RouteToQueryHander(RmContext context, IRmPayload payload, out IRmQueryReply? invocationResult)
        {
            //First we try to invoke functions that match the signature, if that fails we will fall back to invoking the OnNotificationReceived() event.
            if (GetCachedMethod(payload, out var cachedMethod))
            {
                if (GetCachedInstance(cachedMethod, out var cachedInstance))
                {
                    var method = MakeGenericMethodForPayload(cachedMethod, payload);

                    switch (cachedMethod.MethodType)
                    {
                        case CachedMethodType.PayloadOnly:
                            invocationResult = method.Invoke(cachedInstance, [payload]) as IRmQueryReply;
                            return true;
                        case CachedMethodType.PayloadWithContext:
                            invocationResult = method.Invoke(cachedInstance, [context, payload]) as IRmQueryReply;
                            return true;
                    }
                }
            }

            invocationResult = null;

            return false;
        }

        /// <summary>
        /// Calls the appropriate handler function for the given notification payload.
        /// </summary>
        /// <returns>Returns true if the function was found and executed.</returns>
        internal bool RouteToNotificationHander(RmContext context, IRmPayload payload)
        {
            //First we try to invoke functions that match the signature, if that fails we will fall back to invoking the OnNotificationReceived() event.
            if (GetCachedMethod(payload, out var cachedMethod))
            {
                if (GetCachedInstance(cachedMethod, out var cachedInstance))
                {
                    var method = MakeGenericMethodForPayload(cachedMethod, payload);

                    switch (cachedMethod.MethodType)
                    {
                        case CachedMethodType.PayloadOnly:
                            method.Invoke(cachedInstance, [payload]);
                            return true;
                        case CachedMethodType.PayloadWithContext:
                            method.Invoke(cachedInstance, [context, payload]);
                            return true;
                    }
                }
            }

            return false;
        }

        /// <summary>
        /// Creates a cachable and invokable instance of a handler function by matching generic argument types.
        /// </summary>
        private static MethodInfo MakeGenericMethodForPayload(CachedMethod cachedMethod, IRmPayload payload)
        {
            var payloadType = payload.GetType();

            if (Caching.CacheTryGet<MethodInfo>(payloadType, out var cached) && cached != null)
            {
                return cached;
            }

            if (payloadType.IsGenericType)
            {
                // Get the generic type definition and its assembly name
                var typeDefinitionName = payloadType.GetGenericTypeDefinition().FullName
                     ?? throw new Exception("The generic type name is not available.");

                var assemblyName = payloadType.Assembly.FullName
                     ?? throw new Exception("The generic assembly type name is not available.");

                // Recursively get the AssemblyQualifiedName of generic arguments
                var genericTypeArguments = payloadType.GetGenericArguments()
                    .Select(t => Type.GetType(t.AssemblyQualifiedName ?? Reflection.GetAssemblyQualifiedTypeName(t))
                     ?? throw new Exception($"The generic assembly type [{t.AssemblyQualifiedName}] could not be instanciated.")
                    ).ToArray();

                if (genericTypeArguments == null)
                {
                    throw new Exception("The generic assembly type could not be instanciated.");
                }

                var genericMethod = cachedMethod.Method.MakeGenericMethod(genericTypeArguments)
                    ?? throw new Exception("The generic assembly type could not be instanciated.");

                Caching.CacheSetOneMinute(payloadType, genericMethod);

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
        private bool GetCachedInstance(CachedMethod cachedMethod, [NotNullWhen(true)] out IRmMessageHandler? cachedInstance)
        {
            if (cachedMethod.Method.DeclaringType == null)
            {
                cachedInstance = null;
                return false;
                //throw new Exception($"The handler function '{cachedMethod.Name}' does not have a container class.");
            }

            if (_handlerInstances.TryGetValue(cachedMethod.Method.DeclaringType, out cachedInstance))
            {
                return true;
            }

            cachedInstance = Activator.CreateInstance(cachedMethod.Method.DeclaringType) as IRmMessageHandler;
            if (cachedInstance == null)
            {
                return false;
                //throw new Exception($"Failed to instantiate container class '{cachedMethod.DeclaringType.Name}' for handler function '{cachedMethod.Name}'.");
            }
            _handlerInstances.Add(cachedMethod.Method.DeclaringType, cachedInstance);

            return true;
        }

        /// <summary>
        /// Gets the handler function from the pre-loaded handler function cache.
        /// </summary>
        private bool GetCachedMethod(IRmPayload payload, [NotNullWhen(true)] out CachedMethod? cachedMethod)
        {
            var typeName = Reflection.GetAssemblyQualifiedTypeName(payload);

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
                        if (Reflection.ImplementsGenericInterfaceWithArgument(payloadParameter.ParameterType, typeof(IRmQuery<>), typeof(IRmQueryReply)))
                        {
                            if (typeof(IRmQueryReply).IsAssignableFrom(method.ReturnType) == false)
                            {
                                continue; //Query handlers must return a IRMQueryReply type.
                            }
                        }

                        var payloadParameterType = Reflection.GetAssemblyQualifiedTypeName(payloadParameter.ParameterType);
                        _handlerMethods.Add(payloadParameterType, new CachedMethod(CachedMethodType.PayloadOnly, method));
                    }
                }
                else if (parameters.Length == 2)
                {
                    //Notification prototype: void HandleMyNotification(RmContext context, MyNotification notification)
                    //Query prototype:        IReliableMessagingQueryReply HandleMyQuery(RmContext context, MyQuery query)

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
                        if (Reflection.ImplementsGenericInterfaceWithArgument(payloadParameter.ParameterType, typeof(IRmQuery<>), typeof(IRmQueryReply)))
                        {
                            if (typeof(IRmQueryReply).IsAssignableFrom(method.ReturnType) == false)
                            {
                                continue; //Query handlers must return a IRMQueryReply type.
                            }
                        }

                        var payloadParameterType = Reflection.GetAssemblyQualifiedTypeName(payloadParameter.ParameterType);
                        _handlerMethods.Add(payloadParameterType, new CachedMethod(CachedMethodType.PayloadWithContext, method));
                    }
                }
            }
        }
    }
}
