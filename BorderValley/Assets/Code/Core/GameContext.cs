using System;
using System.Collections.Generic;

namespace BorderValley.Core
{
    public sealed class GameContext
    {
        private readonly Dictionary<Type, object> services = new();

        public void Register<T>(T service) where T : class
        {
            if (service == null) throw new ArgumentNullException(nameof(service));
            services[typeof(T)] = service;
        }

        public T Get<T>() where T : class
        {
            if (services.TryGetValue(typeof(T), out var service)) return (T)service;
            throw new InvalidOperationException($"Service {typeof(T).FullName} is not registered.");
        }

        public bool TryGet<T>(out T service) where T : class
        {
            if (services.TryGetValue(typeof(T), out var value))
            {
                service = (T)value;
                return true;
            }
            service = null;
            return false;
        }

        public void Clear() => services.Clear();
    }
}
