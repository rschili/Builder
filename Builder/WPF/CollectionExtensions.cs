using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Data;

namespace RSCoreLib.WPF
    {
    public static class CollectionExtensions
        {
        public static ICollectionView GetCollectionView<T> (this ICollection<T> collection)
            {
            return CollectionViewSource.GetDefaultView(collection);
            }

        public static T GetCurrentItem<T> (this ICollection<T> collection)
            {
            return (T)collection.GetCollectionView().CurrentItem;
            }

        public static bool MoveCurrentItemTo<T> (this ICollection<T> collection, T newCurrentItem)
            {
            return collection.GetCollectionView().MoveCurrentTo(newCurrentItem);
            }

        public static void UpdateWith<T> (this ICollection<T> collection, IEnumerable<T> newValues, Func<T, T, bool> equalityCheck, Action<T, T> updateAction = null, Action<T> afterAddAction = null, bool removeOldValues = false, Action<T> afterRemoveAction = null)
            {
            Func<T, T, bool> defaultEqualityCheck = EqualityComparer<T>.Default.Equals;
            T defaultValue = default(T);

            if (equalityCheck == null)
                equalityCheck = defaultEqualityCheck;

            foreach (var value in newValues)
                {
                var existing = collection.SingleOrDefault(ev => equalityCheck(ev, value));

                if (!defaultEqualityCheck(existing, defaultValue)) //we found a matching element, basically this check means "if existing != default(T)"
                    {
                    if (updateAction != null)
                        updateAction(existing, value);

                    continue;
                    }

                collection.Add(value);
                if (afterAddAction != null)
                    afterAddAction(value);
                }

            if (removeOldValues)
                {
                var oldValues = collection.Where(e => !newValues.Any(nv => equalityCheck(e, nv))).ToList(); //find values from collection which are not in newValues
                foreach (var oldValue in oldValues)
                    {
                    collection.Remove(oldValue);
                    if (afterRemoveAction != null)
                        afterRemoveAction(oldValue);
                    }
                }
            }

        public static void Sort<T,K> (this ObservableCollection<T> observable, Func<T, K> keySelector, bool descending = false) where K : IComparable<K>, IEquatable<K>
            {
            IEnumerable<T> enumerable = descending ? observable.OrderByDescending(keySelector) : observable.OrderBy(keySelector);
            List <T> sorted = enumerable.ToList();

            int ptr = 0;
            while (ptr < sorted.Count)
                {
                if (!observable[ptr].Equals(sorted[ptr]))
                    {
                    T t = observable[ptr];
                    observable.RemoveAt(ptr);
                    observable.Insert(sorted.IndexOf(t), t);
                    }
                else
                    {
                    ptr++;
                    }
                }
            }
        }
    }
