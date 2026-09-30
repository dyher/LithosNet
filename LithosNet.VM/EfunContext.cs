namespace LithosNet.VM {
    // 【Phase 75: 核心架構】Efun 執行上下文，徹底消除全域靜態變數偷渡
    public class EfunContext {
        public ObjectManager ObjMgr { get; }
        public LpcSession Session { get; set; }
        public string CurrentObject { get; } // 當前執行 Efun 的物件 ID (e.g., "login#1")
        public Scope CurrentScope { get; }   // 當前物件的 Scope

        public EfunContext(ObjectManager objMgr, string currentObject, Scope currentScope) {
            ObjMgr = objMgr;
            CurrentObject = currentObject;
            CurrentScope = currentScope;
        }
    }
}
