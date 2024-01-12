using System.Collections.Generic;
using LittleBit.Modules.CoreModule;

namespace ugames.Modules.StorageModule
{
    public interface IDataInfo
    {
        public void UpdateStorage(Dictionary<string, Data> storage);
        public void UpdateData(string key, Data data);
        public void Clear();
    }
}