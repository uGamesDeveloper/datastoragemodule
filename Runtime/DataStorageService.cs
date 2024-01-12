using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using LittleBit.Modules.CoreModule;
using UnityEngine;
using UnityEngine.Scripting;

namespace LittleBit.Modules.StorageModule
{
    public class DataStorageService : IDataStorageService, ISavable
    {
        private readonly Dictionary<string, Data> _storage;
        private readonly ISaveService _saveService;
        private readonly ISaverService _saverService;
        private readonly Dictionary<object, TypedDelegate> _listeners;


        private IDataInfo _infoDataStorageService;
        private Queue<PostRemoveCommand> _postRemoveAllUpdateDataListener;
        private Queue<PostRemoveCommand> _postRemoveUpdateDataListener;

        [Preserve]
        public DataStorageService(ISaveService saveService, ISaverService saverService,
            IDataInfo infoDataStorageService)
        {
            _storage = new Dictionary<string, Data>();
            _saveService = saveService;
            _saverService = saverService;
            _saverService.AddSavableObject(this);
            _infoDataStorageService = infoDataStorageService;
            _infoDataStorageService.Clear();
            _listeners = new Dictionary<object, TypedDelegate>();
            _postRemoveAllUpdateDataListener = new Queue<PostRemoveCommand>();
            _postRemoveUpdateDataListener = new Queue<PostRemoveCommand>();
        }

        public T GetData<T>(string key) where T : Data, new()
        {
            RemoveUnusedListeners();

            if (string.IsNullOrEmpty(key))
                throw new Exception("Key is null or empty");

            if (!_storage.ContainsKey(key))
            {
                T data = _saveService.LoadData<T>(key);
                if (data == null)
                {
                    data = new T();
                }

                _storage.Add(key, data);
            }

            _infoDataStorageService.UpdateData(key, _storage[key]);
            return (T) _storage[key];
        }

        public StorageData<T> CreateDataWrapper<T>(object handler, string key) where T : Data, new()
        {
            return new StorageData<T>(handler, this, key);
        }

        public void SetData<T>(string key, T data, SaveMode saveMode = SaveMode.Save) where T : Data
        {
            RemoveUnusedListeners();
            if (!_storage.ContainsKey(key)) _storage.Add(key, data);
            else _storage[key] = data;

            var type = typeof(T);

            foreach (var obj in _listeners.Keys.ToList())
            {
                if (!_listeners[obj].ContainsKey(type)) continue;

                if (!_listeners[obj][type].ContainsKey(key)) continue;

                foreach (var listener in _listeners[obj][type][key].ToArray())
                {
                    (listener as IDataStorageService.GenericCallback<T>)(data);
                }
            }

            RemoveUnusedListeners();
            
            if(saveMode == SaveMode.Save)
                SaveData(key, data);
            
            _infoDataStorageService.UpdateData(key, data);
        }

        private void RemoveUnusedListeners()
        {
            while (_postRemoveUpdateDataListener.Count > 0)
            {
                PostRemoveCommand command = _postRemoveUpdateDataListener.Dequeue();
                command.List.Remove(command.OnUpdateData);
            }

            while (_postRemoveAllUpdateDataListener.Count > 0)
            {
                PostRemoveCommand command = _postRemoveAllUpdateDataListener.Dequeue();
                command.List.Clear();
            }
        }

        public void AddUpdateDataListener<T>(object handler, string key,
            IDataStorageService.GenericCallback<T> onUpdateData)
        {
            var type = typeof(T);

            if (!_listeners.ContainsKey(handler))
                _listeners[handler] = new TypedDelegate();


            if (!_listeners[handler].ContainsKey(type))
                _listeners[handler][type] = new Dictionary<string, ArrayList>();


            if (!_listeners[handler][type].ContainsKey(key))
                _listeners[handler][type][key] = new ArrayList();

            _listeners[handler][type][key].Add(onUpdateData);
        }

        public void AddUpdateDataListenerWithUpdateData<T>(object handler, string key, IDataStorageService.GenericCallback<T> onUpdateData) where T : Data, new()
        {
            AddUpdateDataListener<T>(handler, key, onUpdateData);
            onUpdateData?.Invoke(GetData<T>(key));
        }

        public void RemoveUpdateDataListener<T>(object handler, string key,
            IDataStorageService.GenericCallback<T> onUpdateData)
        {
            var type = typeof(T);

            if (!_listeners.ContainsKey(handler)) return;

            if (!_listeners[handler].ContainsKey(type)) return;

            if (!_listeners[handler][type].ContainsKey(key)) return;

            if (!_listeners[handler][type][key].Contains(onUpdateData)) return;

            _postRemoveUpdateDataListener.Enqueue(new PostRemoveCommand(_listeners[handler][type][key], onUpdateData));
            _postRemoveUpdateDataListener.Enqueue(new PostRemoveCommand(_listeners[handler][type][key], onUpdateData));
        }

        public void RemoveData<T>(string key) where T : Data
        {
            if (!_storage.ContainsKey(key)) return;

            _storage[key] = null;
        }

        public void RemoveAllUpdateDataListenersOnObject(object handler)
        {
            if (!_listeners.ContainsKey(handler)) return;

            foreach (var type in _listeners[handler].Keys)
            {
                foreach (var key in _listeners[handler][type].Keys)
                {
                    _postRemoveAllUpdateDataListener.Enqueue(
                        new PostRemoveCommand(_listeners[handler][type][key], null));
                }
            }
        }

        private void SaveData(string key, Data value)
        {
            if (value == null) _saveService.ClearData(key);
            else _saveService.SaveData(key, value);
        }
        
        public void Save()
        {
            foreach (var pairData in _storage)
            {
                SaveData(pairData.Key, pairData.Value);
            }
        }


        public class PostRemoveCommand
        {
            private ArrayList _list;
            private object _onUpdateData;

            public PostRemoveCommand(ArrayList list, object onUpdateData)
            {
                _list = list;
                _onUpdateData = onUpdateData;
            }

            public ArrayList List => _list;

            public object OnUpdateData => _onUpdateData;
        }
    }

    public class TypedDelegate : Dictionary<Type, Dictionary<string, ArrayList>>
    {
    }
}