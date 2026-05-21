using System;
using System.Collections.Generic;

namespace MKMTools.Common.Utils
{
    public class Pool<T>
    {
        private readonly Queue<T> _queue;
        private readonly List<T> _activeObjects;
        private readonly Func<T> _create;
        private readonly Action<T> _activate;
        private readonly Action<T> _deactivate;

        public Pool(Func<T> create, Action<T> activate, Action<T> deactivate)
        {
            _queue = new Queue<T>();
            _activeObjects = new List<T>();
            _create = create;
            _activate = activate;
            _deactivate = deactivate;
        }

        public T PoolObject()
        {
            T image = _queue.Count > 0 ? _queue.Dequeue() : _create.Invoke();
            _activate.Invoke(image);
            _activeObjects.Add(image);
            return image;
        }

        public void Deactivate()
        {
            foreach (var activeObject in _activeObjects)
            {
                _deactivate.Invoke(activeObject);
                _queue.Enqueue(activeObject);
            }

            _activeObjects.Clear();
        }
    }
}