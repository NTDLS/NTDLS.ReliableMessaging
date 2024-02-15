using System.Diagnostics.CodeAnalysis;
using System.Reflection;

namespace NTDLS.ReliableMessaging.Internal
{
    /// <summary>
    /// Manages class instances and method reflection information for message handlers.
    /// </summary>
    public class ReflectionCache
    {
        private readonly Dictionary<Type, MethodInfo> _methodCache = new();
        private readonly Dictionary<Type, IRmMessageHandler> _instanceCache = new();

        internal void AddInstance(IRmMessageHandler handlerClass)
        {
            _instanceCache.Add(handlerClass.GetType(), handlerClass);

            CacheConventionBasedEventingMethods(handlerClass);
        }

        internal bool GetCachedMethod(Type type, [NotNullWhen(true)] out MethodInfo? cachedMethod)
        {
            if (_methodCache.TryGetValue(type, out cachedMethod) == false)
            {
                return false;
                //throw new Exception($"A handler function for type '{type.Name}' was not found in the assembly cache.");
            }

            if (cachedMethod?.DeclaringType == null)
            {
                return false;
                //throw new Exception($"A handler function for type '{type.Name}' was found, but it is not in class that can be instantiated.");
            }

            return true;
        }

        internal bool GetCachedInstance(MethodInfo cachedMethod, [NotNullWhen(true)] out IRmMessageHandler? cachedInstance)
        {
            if (cachedMethod.DeclaringType == null)
            {
                cachedInstance = null;
                return false;
                //throw new Exception($"The handler function '{cachedMethod.Name}' does not have a container class.");
            }

            if (_instanceCache.TryGetValue(cachedMethod.DeclaringType, out cachedInstance))
            {
                return true;
            }

            cachedInstance = Activator.CreateInstance(cachedMethod.DeclaringType) as IRmMessageHandler;
            if (cachedInstance == null)
            {
                return false;
                //throw new Exception($"Failed to instantiate container class '{cachedMethod.DeclaringType.Name}' for handler function '{cachedMethod.Name}'.");
            }
            _instanceCache.Add(cachedMethod.DeclaringType, cachedInstance);

            return true;
        }

        internal void CacheConventionBasedEventingMethods(IRmMessageHandler handlerClass)
        {
            foreach (var method in handlerClass.GetType().GetMethods())
            {
                var parameters = method.GetParameters();
                if (parameters.Count() == 2)
                {
                    //Notification prototype: void HandleMyNotification(IReliableMessagingEndpoint endpoint, Guid connectionId, MyNotification notification)
                    //Query prototype:        IReliableMessagingQueryReply HandleMyQuery(IReliableMessagingEndpoint endpoint, Guid connectionId, MyQuery query)

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

                        _methodCache.Add(payloadParameter.ParameterType, method);
                    }
                }
            }
        }
    }
}
