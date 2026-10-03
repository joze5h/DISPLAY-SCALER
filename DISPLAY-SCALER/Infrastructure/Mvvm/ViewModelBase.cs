using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace DISPLAY_SCALER.Infrastructure.Mvvm
{
    internal static class PropertyChangedEventArgsCache
    {
        private static readonly PropertyChangedEventArgs NullArgs = new PropertyChangedEventArgs(null);
        private static readonly Dictionary<string, PropertyChangedEventArgs> Cache = new Dictionary<string, PropertyChangedEventArgs>(StringComparer.Ordinal);
        private static readonly object SyncRoot = new object();

        public static PropertyChangedEventArgs Get(string propertyName)
        {
            if (propertyName == null)
                return NullArgs;

            lock (SyncRoot)
            {
                if (!Cache.TryGetValue(propertyName, out var args))
                {
                    args = new PropertyChangedEventArgs(propertyName);
                    Cache.Add(propertyName, args);
                }

                return args;
            }
        }
    }

    public abstract class ViewModelBase : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        protected bool Set<T>(ref T field, T value, [CallerMemberName] string propertyName = null)
        {
            if (EqualityComparer<T>.Default.Equals(field, value)) return false;
            field = value;
            OnPropertyChanged(propertyName);
            return true;
        }

        protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
            => PropertyChanged?.Invoke(this, PropertyChangedEventArgsCache.Get(propertyName));
    }
}