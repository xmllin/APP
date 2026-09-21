using System;
using System.Collections.Generic;
using System.Windows.Controls;

namespace WpfApp1.Application.Navigation
{
    public sealed class NavigationService
    {
        private readonly Dictionary<string, Func<UserControl>> _routes = new Dictionary<string, Func<UserControl>>(StringComparer.OrdinalIgnoreCase);

        public void Register(string key, Func<UserControl> factory)
        {
            if (string.IsNullOrWhiteSpace(key)) throw new ArgumentException("Ключ страницы не задан.", nameof(key));
            if (factory == null) throw new ArgumentNullException(nameof(factory));
            _routes[key] = factory;
        }

        public UserControl Resolve(string key)
        {
            return key != null && _routes.TryGetValue(key, out var factory) ? factory() : null;
        }
    }
}
