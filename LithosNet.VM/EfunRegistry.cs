using System;
using System.Collections.Generic;
using System.Reflection;
using LithosNet.Core;

namespace LithosNet.VM {
    // 定義 Efun 的統一簽名：接收 LpcValue 陣列，返回 LpcValue
    public delegate LpcValue EfunDelegate(LpcValue[] args);

    public static class EfunRegistry {
        private static readonly Dictionary<string, EfunDelegate> _efuns = new();

        // 啟動時掃描指定的類別，自動註冊所有帶 [Efun] 標籤的方法
        public static void RegisterFromType(Type type) {
            foreach (var method in type.GetMethods(BindingFlags.Public | BindingFlags.Static)) {
                var attr = method.GetCustomAttribute<EfunAttribute>();
                if (attr != null) {
                    var del = (EfunDelegate)Delegate.CreateDelegate(typeof(EfunDelegate), method);
                    _efuns[attr.Name] = del;
                    Console.WriteLine($"   🔌 [Efun] 已註冊底層函數: {attr.Name}()");
                }
            }
        }

        public static bool TryGet(string name, out EfunDelegate del) => _efuns.TryGetValue(name, out del);
    }
}
