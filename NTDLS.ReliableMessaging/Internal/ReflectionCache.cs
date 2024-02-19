using System.Diagnostics.CodeAnalysis;
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
            /// The method has only a payload parameter.
            /// </summary>
            PayloadOnly,
            /// <summary>
            /// The method has both a context and a payload parameter.
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
            /// Creates a new instace of the CachedMethod class.
            /// </summary>
            /// <param name="methodType"></param>
            /// <param name="method"></param>
            public CachedMethod(CachedMethodType methodType, MethodInfo method)
            {
                MethodType = methodType;
                Method = method;
            }
        }

        private readonly Dictionary<Type, CachedMethod> _methodCache = new();
        private readonly Dictionary<Type, IRmMessageHandler> _instanceCache = new();

        internal void AddInstance(IRmMessageHandler handlerClass)
        {
            _instanceCache.Add(handlerClass.GetType(), handlerClass);

            CacheConventionBasedEventingMethods(handlerClass);
        }

        internal bool GetCachedMethod(Type type, [NotNullWhen(true)] out CachedMethod? cachedMethod)
        {
            if (_methodCache.TryGetValue(type, out cachedMethod) == false)
            {
                return false;
                //throw new Exception($"A handler function for type '{type.Name}' was not found in the assembly cache.");
            }

            if (cachedMethod.Method.DeclaringType == null)
            {
                return false;
                //throw new Exception($"A handler function for type '{type.Name}' was found, but it is not in class that can be instantiated.");
            }

            return true;
        }

        internal bool GetCachedInstance(CachedMethod cachedMethod, [NotNullWhen(true)] out IRmMessageHandler? cachedInstance)
        {
            if (cachedMethod.Method.DeclaringType == null)
            {
                cachedInstance = null;
                return false;
                //throw new Exception($"The handler function '{cachedMethod.Name}' does not have a container class.");
            }

            if (_instanceCache.TryGetValue(cachedMethod.Method.DeclaringType, out cachedInstance))
            {
                return true;
            }

            cachedInstance = Activator.CreateInstance(cachedMethod.Method.DeclaringType) as IRmMessageHandler;
            if (cachedInstance == null)
            {
                return false;
                //throw new Exception($"Failed to instantiate container class '{cachedMethod.DeclaringType.Name}' for handler function '{cachedMethod.Name}'.");
            }
            _instanceCache.Add(cachedMethod.Method.DeclaringType, cachedInstance);

            return true;
        }

        internal void CacheConventionBasedEventingMethods(IRmMessageHandler handlerClass)
        {
            foreach (var method in handlerClass.GetType().GetMethods())
            {
                var parameters = method.GetParameters();
                if (parameters.Length == 1)
                {
                    //Notification prototype: void HandleMyNotification(MyNotification notification)
                    //Query prototype:        IReliableMessagingQueryReply HandleMyQuery(MyQuery query)

                    if (typeof(IRmPayload).IsAssignableFrom(parameters[0].ParameterType) == false)
                    {
                        continue;
                    }

                    var payloadParameter = parameters[0];
                    if (payloadParameter != null)
                    {
                        //If the payload parameter is a IReliableMessagingQuery, then ensure that the return type is a IReliableMessagingQueryReply.
                        if (Utility.ImplementsGenericInterfaceWithArgument(payloadParameter.ParameterType, typeof(IRmQuery<>), typeof(IRmQueryReply)))
                        {
                            if (typeof(IRmQueryReply).IsAssignableFrom(method.ReturnType) == false)
                            {
                                continue; //Query handlers must return a IRMQueryReply type.
                            }
                        }

                        _methodCache.Add(payloadParameter.ParameterType, new CachedMethod(CachedMethodType.PayloadOnly, method));
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
                        if (Utility.ImplementsGenericInterfaceWithArgument(payloadParameter.ParameterType, typeof(IRmQuery<>), typeof(IRmQueryReply)))
                        {
                            if (typeof(IRmQueryReply).IsAssignableFrom(method.ReturnType) == false)
                            {
                                continue; //Query handlers must return a IRMQueryReply type.
                            }
                        }

                        _methodCache.Add(payloadParameter.ParameterType, new CachedMethod(CachedMethodType.PayloadWithContext, method));
                    }
                }
            }
        }
    }
}
